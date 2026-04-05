using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Optimization.Models;
using TradingApp.Application.Optimization.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Domain.Trading;
using System.Collections.Concurrent;

namespace TradingApp.Application.Tests.Optimization;

[TestClass]
public sealed class SweepRunnerTests
{
    private readonly Mock<IBacktestRunner> _backtestRunner = new();
    private readonly Mock<IStrategyConfigGenerator> _configGenerator = new();
    private readonly Mock<IFitnessScorer> _fitnessScorer = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
    private readonly Mock<ICandleRepository> _candleRepository = new();

    [TestMethod]
    public async Task GivenValidConfig_WhenRunAsync_ThenCallsBacktestRunnerForEachStrategy()
    {
        var strategies = CreateStrategies(4);
        var results = strategies.ToDictionary(strategy => strategy.Config.StrategyName, _ => CreateResult());

        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 4, null))
            .Returns(strategies);
        _fitnessScorer
            .Setup(scorer => scorer.IsQualified(It.IsAny<BacktestResult>(), It.IsAny<FitnessThresholds>(), 10_000m))
            .Returns(true);
        _fitnessScorer
            .Setup(scorer => scorer.Score(It.IsAny<BacktestResult>()))
            .Returns(100m);
        _backtestRunner
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BacktestConfig config, CancellationToken _) => results[((StrategyConfig)config.Strategy).StrategyName]);

        var runner = CreateRunner();

        var sweepResult = await runner.RunAsync(CreateSweepConfig(sampleSize: 4));

        sweepResult.TotalRun.Should().Be(4);
        _backtestRunner.Verify(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [TestMethod]
    public async Task GivenQualifiedResults_WhenRunAsync_ThenReturnsTop10RankedByFitness()
    {
        var strategies = CreateStrategies(12);
        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 12, null))
            .Returns(strategies);
        _fitnessScorer
            .Setup(scorer => scorer.IsQualified(It.IsAny<BacktestResult>(), It.IsAny<FitnessThresholds>(), 10_000m))
            .Returns(true);
        _fitnessScorer
            .Setup(scorer => scorer.Score(It.IsAny<BacktestResult>()))
            .Returns((BacktestResult result) => result.TotalPnL);
        _backtestRunner
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BacktestConfig config, CancellationToken _) =>
            {
                var index = int.Parse(((StrategyConfig)config.Strategy).StrategyName.Split('-').Last());
                return CreateResult(totalPnl: index);
            });

        var runner = CreateRunner();

        var sweepResult = await runner.RunAsync(CreateSweepConfig(sampleSize: 12));

        sweepResult.TopResults.Should().HaveCount(12);
        sweepResult.TopResults.Select(result => result.FitnessScore).Should().BeInDescendingOrder();
        sweepResult.TopResults.First().FitnessScore.Should().Be(12m);
        sweepResult.TopResults.Last().FitnessScore.Should().Be(1m);
    }

    [TestMethod]
    public async Task GivenNoQualifiedResults_WhenRunAsync_ThenReturnsEmptyTopResults()
    {
        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 3, null))
            .Returns(CreateStrategies(3));
        _fitnessScorer
            .Setup(scorer => scorer.IsQualified(It.IsAny<BacktestResult>(), It.IsAny<FitnessThresholds>(), 10_000m))
            .Returns(false);
        _backtestRunner
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResult());

        var runner = CreateRunner();

        var sweepResult = await runner.RunAsync(CreateSweepConfig(sampleSize: 3));

        sweepResult.TopResults.Should().BeEmpty();
        sweepResult.TotalQualified.Should().Be(0);
    }

    [TestMethod]
    public async Task GivenProgressCallback_WhenRunAsync_ThenReportsProgressIncrementally()
    {
        var progressUpdates = new ConcurrentBag<SweepProgress>();

        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 5, null))
            .Returns(CreateStrategies(5));
        _fitnessScorer
            .Setup(scorer => scorer.IsQualified(It.IsAny<BacktestResult>(), It.IsAny<FitnessThresholds>(), 10_000m))
            .Returns(true);
        _fitnessScorer
            .Setup(scorer => scorer.Score(It.IsAny<BacktestResult>()))
            .Returns(1m);
        _backtestRunner
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResult());

        var runner = CreateRunner();

        await runner.RunAsync(CreateSweepConfig(sampleSize: 5), progress => progressUpdates.Add(progress));

        progressUpdates.Should().HaveCount(5);
        progressUpdates.Any(update => update.Completed == 5 && update.Total == 5).Should().BeTrue();
        progressUpdates.Select(update => update.Completed).Should().BeEquivalentTo([1, 2, 3, 4, 5]);
        progressUpdates.Should().AllSatisfy(update => update.Phase.Should().NotBeNullOrEmpty());
    }

    [TestMethod]
    public async Task GivenCancellationRequested_WhenRunAsync_ThenThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 5, null))
            .Returns(CreateStrategies(5));

        var runner = CreateRunner();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => runner.RunAsync(CreateSweepConfig(sampleSize: 5), cancellationToken: cts.Token));
    }

    [TestMethod]
    public async Task GivenBacktestFailsForSome_WhenRunAsync_ThenContinuesWithRemainingStrategies()
    {
        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 4, null))
            .Returns(CreateStrategies(4));
        _fitnessScorer
            .Setup(scorer => scorer.IsQualified(It.IsAny<BacktestResult>(), It.IsAny<FitnessThresholds>(), 10_000m))
            .Returns(true);
        _fitnessScorer
            .Setup(scorer => scorer.Score(It.IsAny<BacktestResult>()))
            .Returns(10m);
        _backtestRunner
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BacktestConfig config, CancellationToken _) =>
            {
                var strategyName = ((StrategyConfig)config.Strategy).StrategyName;
                if (strategyName == "Strategy-2")
                {
                    throw new InvalidOperationException("boom");
                }

                return CreateResult();
            });

        var runner = CreateRunner();

        var sweepResult = await runner.RunAsync(CreateSweepConfig(sampleSize: 4));

        sweepResult.TotalRun.Should().Be(4);
        sweepResult.TotalQualified.Should().Be(3);
    }

    [TestMethod]
    public async Task GivenMoreThan25Qualified_WhenRunAsync_ThenReturnsOnlyTop25()
    {
        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 30, null))
            .Returns(CreateStrategies(30));
        _fitnessScorer
            .Setup(scorer => scorer.IsQualified(It.IsAny<BacktestResult>(), It.IsAny<FitnessThresholds>(), 10_000m))
            .Returns(true);
        _fitnessScorer
            .Setup(scorer => scorer.Score(It.IsAny<BacktestResult>()))
            .Returns((BacktestResult result) => result.TotalPnL);
        _backtestRunner
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BacktestConfig config, CancellationToken _) =>
            {
                var index = int.Parse(((StrategyConfig)config.Strategy).StrategyName.Split('-').Last());
                return CreateResult(totalPnl: index);
            });

        var runner = CreateRunner();

        var sweepResult = await runner.RunAsync(CreateSweepConfig(sampleSize: 30));

        sweepResult.TopResults.Should().HaveCount(25);
    }

    [TestMethod]
    public async Task GivenWalkForwardEnabled_WhenRunAsync_ThenTopResultsHaveOutOfSampleMetrics()
    {
        var strategies = CreateStrategies(5);
        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 5, null))
            .Returns(strategies);
        _fitnessScorer
            .Setup(scorer => scorer.IsQualified(It.IsAny<BacktestResult>(), It.IsAny<FitnessThresholds>(), 10_000m))
            .Returns(true);
        _fitnessScorer
            .Setup(scorer => scorer.Score(It.IsAny<BacktestResult>()))
            .Returns(50m);
        _backtestRunner
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResult());

        var runner = CreateRunner();
        var config = CreateSweepConfig(sampleSize: 5) with
        {
            StartDateUtc = 1000,
            EndDateUtc = 2000,
            WalkForward = new WalkForwardConfig { Enabled = true, ValidationSplitPercent = 30m },
        };

        var sweepResult = await runner.RunAsync(config);

        sweepResult.TopResults.Should().NotBeEmpty();
        sweepResult.TopResults.Should().AllSatisfy(r => r.OutOfSample.Should().NotBeNull());
    }

    [TestMethod]
    public async Task GivenWalkForwardDisabled_WhenRunAsync_ThenTopResultsHaveNoOutOfSampleMetrics()
    {
        var strategies = CreateStrategies(3);
        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 3, null))
            .Returns(strategies);
        _fitnessScorer
            .Setup(scorer => scorer.IsQualified(It.IsAny<BacktestResult>(), It.IsAny<FitnessThresholds>(), 10_000m))
            .Returns(true);
        _fitnessScorer
            .Setup(scorer => scorer.Score(It.IsAny<BacktestResult>()))
            .Returns(50m);
        _backtestRunner
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResult());

        var runner = CreateRunner();

        var sweepResult = await runner.RunAsync(CreateSweepConfig(sampleSize: 3));

        sweepResult.TopResults.Should().NotBeEmpty();
        sweepResult.TopResults.Should().AllSatisfy(r => r.OutOfSample.Should().BeNull());
    }

    [TestMethod]
    public async Task GivenWalkForwardEnabled_WhenRunAsync_ThenInSampleUsesReducedDateRange()
    {
        var capturedConfigs = new ConcurrentBag<BacktestConfig>();
        var strategies = CreateStrategies(2);
        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 2, null))
            .Returns(strategies);
        _fitnessScorer
            .Setup(scorer => scorer.IsQualified(It.IsAny<BacktestResult>(), It.IsAny<FitnessThresholds>(), 10_000m))
            .Returns(true);
        _fitnessScorer
            .Setup(scorer => scorer.Score(It.IsAny<BacktestResult>()))
            .Returns(50m);
        _backtestRunner
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BacktestConfig config, CancellationToken _) =>
            {
                capturedConfigs.Add(config);
                return CreateResult();
            });

        var runner = CreateRunner();
        var config = CreateSweepConfig(sampleSize: 2) with
        {
            StartDateUtc = 0,
            EndDateUtc = 10_000,
            WalkForward = new WalkForwardConfig { Enabled = true, ValidationSplitPercent = 30m },
        };

        await runner.RunAsync(config);

        // In-sample should end at 7000 (70% of 10000), OOS should start at 7000
        var inSampleConfigs = capturedConfigs.Where(c => c.EndDateUtc == 7000).ToList();
        var oosConfigs = capturedConfigs.Where(c => c.StartDateUtc == 7000).ToList();
        inSampleConfigs.Should().HaveCount(2, "both strategies should be backtested on in-sample");
        oosConfigs.Should().HaveCount(2, "both top strategies should be validated on OOS period");
    }

    [TestMethod]
    public async Task GivenEvolutionaryEnabled_WhenRunAsync_ThenRunsMoreBacktestsThanSampleSize()
    {
        var strategies = CreateStrategies(5);
        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 5, null))
            .Returns(strategies);
        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 1, It.IsAny<int?>()))
            .Returns((string _, ParameterBounds _, int _, int? seed) =>
                new List<GeneratedStrategy> { strategies[0] with { Description = $"Donor-{seed}" } });
        _fitnessScorer
            .Setup(scorer => scorer.IsQualified(It.IsAny<BacktestResult>(), It.IsAny<FitnessThresholds>(), 10_000m))
            .Returns(true);
        _fitnessScorer
            .Setup(scorer => scorer.Score(It.IsAny<BacktestResult>()))
            .Returns(50m);
        _backtestRunner
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResult());

        var runner = CreateRunner();
        var config = CreateSweepConfig(sampleSize: 5) with
        {
            Evolutionary = new EvolutionaryConfig { Enabled = true, Generations = 2, EliteCount = 3, CrossoverRate = 0.7m, MutationRate = 0.3m },
        };

        var sweepResult = await runner.RunAsync(config);

        // Initial 5 + (2 generations × 5 offspring each) = 15 total runs
        sweepResult.TotalRun.Should().BeGreaterThan(5);
    }

    [TestMethod]
    public async Task GivenEvolutionaryWithFewerThan2Elites_WhenRunAsync_ThenSkipsEvolution()
    {
        var strategies = CreateStrategies(3);
        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 3, null))
            .Returns(strategies);
        _fitnessScorer
            .Setup(scorer => scorer.IsQualified(It.IsAny<BacktestResult>(), It.IsAny<FitnessThresholds>(), 10_000m))
            .Returns(false); // No qualified → no elites → evolution skipped
        _backtestRunner
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResult());

        var runner = CreateRunner();
        var config = CreateSweepConfig(sampleSize: 3) with
        {
            Evolutionary = new EvolutionaryConfig { Enabled = true, Generations = 3, EliteCount = 5 },
        };

        var sweepResult = await runner.RunAsync(config);

        // Only initial sweep was run (3 strategies)
        sweepResult.TotalRun.Should().Be(3);
    }

    private SweepRunner CreateRunner()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(sp => sp.GetService(typeof(IBacktestRunner)))
            .Returns(_backtestRunner.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        // IServiceScope implements IDisposable, and CreateAsyncScope wraps it as
        // an AsyncServiceScope (IAsyncDisposable). Mock both interfaces.
        _scopeFactory
            .Setup(f => f.CreateScope())
            .Returns(scope.Object);

        _fitnessScorer
            .Setup(scorer => scorer.ComputeMetrics(It.IsAny<BacktestResult>()))
            .Returns(new FitnessMetrics());

        return new SweepRunner(_scopeFactory.Object, _candleRepository.Object, _configGenerator.Object, _fitnessScorer.Object, NullLogger<SweepRunner>.Instance);
    }

    private static SweepConfig CreateSweepConfig(int sampleSize)
    {
        return new SweepConfig
        {
            Symbol = "BTC",
            StartDateUtc = 1,
            EndDateUtc = 2,
            InitialCapital = 10_000m,
            SampleSize = sampleSize,
        };
    }

    private static IReadOnlyList<GeneratedStrategy> CreateStrategies(int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new GeneratedStrategy(
                new StrategyConfig
                {
                    SchemaVersion = 1,
                    StrategyMode = StrategyMode.Signal,
                    StrategyName = $"Strategy-{index}",
                    Exchange = "Hyperliquid",
                    Market = "BTC",
                    Timeframe = "15m",
                    Direction = Direction.Long,
                    Enabled = true,
                    EntryLogic = EntryLogic.All,
                    EntryConditions =
                    [
                        new EntryConditionConfig
                        {
                            Id = $"rsi-{index}",
                            Enabled = true,
                            Type = EntryConditionType.Rsi,
                            Label = "RSI",
                            Params = new RsiParams
                            {
                                Period = 14,
                                Operator = "lt",
                                Value = 40m,
                            },
                        },
                    ],
                    Exit = new ExitConfig
                    {
                        TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 4m },
                        StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
                    },
                    Risk = new RiskConfig
                    {
                        PositionSizeType = PositionSizeType.PercentWallet,
                        PositionSizeValue = 10m,
                        Leverage = 3m,
                        MaxOpenTrades = 1,
                        CooldownValue = 1,
                        CooldownUnit = CooldownUnit.Candles,
                        AllowSameCandleReentry = false,
                    },
                },
                $"Strategy {index}"))
            .ToList();
    }

    private static BacktestResult CreateResult(decimal totalPnl = 100m)
    {
        return new BacktestResult
        {
            TotalTrades = 20,
            WinningTrades = 12,
            LosingTrades = 8,
            WinRate = 60m,
            TotalPnL = totalPnl,
            MaxDrawdownAbsolute = 500m,
            MaxDrawdownPercent = 5m,
            AverageTradePnL = 5m,
            AverageHoldTime = TimeSpan.FromMinutes(30),
            HedgesOpened = 0,
            TotalFeesPaid = 10m,
            GridCycles = 0,
            CandlesReplayed = 1000,
            FinalEquity = 10_100m,
            EquityTimeSeries = [],
            TradeLog = [],
        };
    }
}