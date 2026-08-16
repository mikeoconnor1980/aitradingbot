using System.Text.Json;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Backtesting.Experiments;
using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Tests.Backtesting.Experiments;

[TestClass]
public sealed class BacktestExperimentServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private readonly Mock<IStrategyRepository> _strategyRepository = new();
    private readonly Mock<IStrategyRevisionRepository> _revisionRepository = new();
    private readonly Mock<IBacktestRunner> _backtestRunner = new();

    [TestMethod]
    public async Task GivenRsiCandidate_WhenExperimentRuns_ThenBaselineConfigIsNotMutatedAndResultsAreCompared()
    {
        var strategy = CreateStrategy();
        var revision = CreateRevision(strategy);
        var capturedConfigs = new List<BacktestConfig>();
        var results = new Queue<BacktestResult>(
        [
            CreateResult(totalPnl: 100m, maxDrawdown: 20m, totalTrades: 4, profitFactor: 2m),
            CreateResult(totalPnl: 140m, maxDrawdown: 24m, totalTrades: 6, profitFactor: 2.5m),
        ]);
        ConfigureBaseStrategy(strategy, revision);
        _backtestRunner
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .Callback<BacktestConfig, CancellationToken>((config, _) => capturedConfigs.Add(config))
            .ReturnsAsync(() => results.Dequeue());
        var sut = CreateSut();

        var result = await sut.RunAsync(CreateRequest("65") with { StrategyId = strategy.Id }, CancellationToken.None);

        result.Baseline.TotalPnl.Should().Be(100m);
        result.Candidates[0].Comparison.TotalPnlDelta.Should().Be(40m);
        result.Candidates[0].Comparison.MaxDrawdownAbsoluteDelta.Should().Be(4m);
        capturedConfigs.Should().HaveCount(2);
        ((RsiParams)((StrategyConfig)capturedConfigs[0].Strategy).EntryConditions![0].Params!).Value.Should().Be(62m);
        ((RsiParams)((StrategyConfig)capturedConfigs[1].Strategy).EntryConditions![0].Params!).Value.Should().Be(65m);
        JsonSerializer.Deserialize<StrategyConfig>(revision.ConfigJson, StrategyJsonOptions.Default)!
            .EntryConditions![0].Params.Should().BeOfType<RsiParams>().Which.Value.Should().Be(62m);
        _strategyRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenDuplicateOrOversizedExperiment_WhenValidated_ThenBacktestIsNotRun()
    {
        var sut = CreateSut();
        var duplicate = CreateRequest("65") with
        {
            Candidates =
            [
                CreateCandidate("65", 65m),
                CreateCandidate("65 again", 65m),
            ],
        };

        var action = () => sut.RunAsync(duplicate, CancellationToken.None);

        await action.Should().ThrowAsync<Exception>().WithMessage("*must not duplicate*");
        _backtestRunner.Verify(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void GivenZeroTradeOrUndefinedProfitFactor_WhenCompared_ThenDeltasRemainDeterministicWithoutInventingInfinity()
    {
        var baseline = new BacktestExperimentMetrics(0m, 0m, 0m, 0, 0m, null, 0m, 0m, 10);
        var candidate = new BacktestExperimentMetrics(0m, 0m, 0m, 0, 0m, null, 0m, 0m, 10);

        var comparison = BacktestComparison.Between(baseline, candidate);

        comparison.TotalTradesDelta.Should().Be(0);
        comparison.TotalPnlDelta.Should().Be(0m);
        comparison.ProfitFactorDelta.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenUnknownParameterOrRegimeFilter_WhenExperimentRuns_ThenRequestIsRejectedBeforeExecution()
    {
        var strategy = CreateStrategy();
        ConfigureBaseStrategy(strategy, CreateRevision(strategy));
        var sut = CreateSut();
        var unknown = CreateRequest("65") with
        {
            StrategyId = strategy.Id,
            Candidates =
            [new BacktestCandidateRequest("Unknown", [new StrategyParameterOverride("risk.leverage", "rsi-1", 2m)])],
        };
        var filtered = CreateRequest("65") with
        {
            StrategyId = strategy.Id,
            RegimeFilter = new BacktestRegimeFilter("4h", "bullish"),
        };

        Func<Task> unknownAction = () => sut.RunAsync(unknown);
        Func<Task> filteredAction = () => sut.RunAsync(filtered);

        await unknownAction.Should().ThrowAsync<Exception>().WithMessage("*not an experiment-configurable parameter*");
        await filteredAction.Should().ThrowAsync<Exception>().WithMessage("*Regime-filtered*");
        _backtestRunner.Verify(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private BacktestExperimentService CreateSut() => new(
        _strategyRepository.Object,
        _revisionRepository.Object,
        _backtestRunner.Object);

    private void ConfigureBaseStrategy(Strategy strategy, StrategyRevision revision)
    {
        _strategyRepository.Setup(repository => repository.GetByIdAsync(strategy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategy);
        _revisionRepository.Setup(repository => repository.GetLatestRevisionNumberAsync(strategy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _revisionRepository.Setup(repository => repository.GetByStrategyAndRevisionAsync(strategy.Id, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(revision);
    }

    private static BacktestExperimentRequest CreateRequest(string label) => new(
        Guid.Parse("11111111-2222-3333-4444-555555555555"),
        1,
        "BTC",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
        10_000m,
        [CreateCandidate(label, 65m)],
        UserId.ToString());

    private static BacktestCandidateRequest CreateCandidate(string label, decimal value) => new(
        label,
        [new StrategyParameterOverride(BacktestExperimentService.RsiValueParameter, "rsi-1", value)]);

    private static Strategy CreateStrategy()
    {
        var config = new StrategyConfig
        {
            StrategyName = "v10.4",
            StrategyMode = StrategyMode.Signal,
            Market = "BTC",
            EntryConditions =
            [new EntryConditionConfig
            {
                Id = "rsi-1",
                Enabled = true,
                Type = EntryConditionType.Rsi,
                Label = "RSI maximum",
                Params = new RsiParams { Period = 14, Operator = "less_than", Value = 62m },
            }],
        };
        return Strategy.Create(UserId.ToString(), "v10.4", "signal", JsonSerializer.Serialize(config, StrategyJsonOptions.Default));
    }

    private static StrategyRevision CreateRevision(Strategy strategy) => StrategyRevision.Create(
        strategy.Id,
        1,
        strategy.ConfigJson,
        RevisionSource.Ui,
        "Initial revision");

    private static BacktestResult CreateResult(
        decimal totalPnl = 0m,
        decimal maxDrawdown = 0m,
        int totalTrades = 0,
        decimal? profitFactor = null) => new()
    {
        TotalTrades = totalTrades,
        WinningTrades = totalTrades,
        LosingTrades = 0,
        WinRate = totalTrades == 0 ? 0m : 100m,
        TotalPnL = totalPnl,
        MaxDrawdownAbsolute = maxDrawdown,
        MaxDrawdownPercent = maxDrawdown / 100m,
        AverageTradePnL = totalTrades == 0 ? 0m : totalPnl / totalTrades,
        AverageHoldTime = TimeSpan.Zero,
        HedgesOpened = 0,
        TotalFeesPaid = 0m,
        GridCycles = 0,
        CandlesReplayed = 10,
        FinalEquity = 10_000m + totalPnl,
        ProfitFactor = profitFactor,
        EquityTimeSeries = [],
        TradeLog = [],
    };
}