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

        sweepResult.TopResults.Should().HaveCount(10);
        sweepResult.TopResults.Select(result => result.FitnessScore).Should().BeInDescendingOrder();
        sweepResult.TopResults.First().FitnessScore.Should().Be(12m);
        sweepResult.TopResults.Last().FitnessScore.Should().Be(3m);
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
        var progressUpdates = new ConcurrentBag<(int Completed, int Total)>();

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

        await runner.RunAsync(CreateSweepConfig(sampleSize: 5), (completed, total) => progressUpdates.Add((completed, total)));

        progressUpdates.Should().HaveCount(5);
        progressUpdates.Any(update => update.Completed == 5 && update.Total == 5).Should().BeTrue();
        progressUpdates.Select(update => update.Completed).Should().BeEquivalentTo([1, 2, 3, 4, 5]);
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

        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => runner.RunAsync(CreateSweepConfig(sampleSize: 5), cancellationToken: cts.Token));
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
    public async Task GivenMoreThan10Qualified_WhenRunAsync_ThenReturnsOnlyTop10()
    {
        _configGenerator
            .Setup(generator => generator.Generate("BTC", It.IsAny<ParameterBounds>(), 15, null))
            .Returns(CreateStrategies(15));
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

        var sweepResult = await runner.RunAsync(CreateSweepConfig(sampleSize: 15));

        sweepResult.TopResults.Should().HaveCount(10);
    }

    private SweepRunner CreateRunner()
    {
        return new SweepRunner(_backtestRunner.Object, _configGenerator.Object, _fitnessScorer.Object);
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