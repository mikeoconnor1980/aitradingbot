using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Trading.Services;

[TestClass]
public sealed class GridControllerTests
{
    private const long CandleTimestamp = 1_000_000;
    private const string DefaultConfigJson = """
        {"gridLevels":5,"gridSpacing":0.5,"takeProfitPercent":1,"breakdownThreshold":2,"makerFee":0.0001,"takerFee":0.00035,"slippage":0,"positionSize":100,"leverage":3,"stopLossPercent":5}
        """;

    private GridController _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new GridController();
    }

    [TestMethod]
    public async Task GivenNoSetupAndNoPosition_WhenProcessAsync_ThenReturnsEmptySignals()
    {
        var signals = await _sut.ProcessAsync(
            CreateEvaluation(setupDetected: false),
            CreateMarketContext(close: 100m),
            CreateGridState(GridLifecycle.Inactive),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            DefaultConfigJson);

        signals.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenInactiveGrid_WhenSetupDetectedAndNoPosition_ThenEmitsDeployGrid()
    {
        var gridState = CreateGridState(GridLifecycle.Inactive, totalLevels: 0);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m),
            gridState,
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            DefaultConfigJson);

        signals.Should().ContainSingle();
        signals[0].SignalType.Should().Be("DeployGrid");
        gridState.Lifecycle.Should().Be(GridLifecycle.Deploying);
        gridState.TotalLevels.Should().Be(5);
        gridState.FilledLevels.Should().Be(0);
        gridState.GridCycleId.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public async Task GivenPartiallyFilledGridAndNoPosition_WhenSetupDetected_ThenDoesNotRedeploy()
    {
        var gridState = CreateGridState(GridLifecycle.PartiallyFilled, filledLevels: 2);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m),
            gridState,
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            DefaultConfigJson);

        signals.Should().BeEmpty();
        gridState.Lifecycle.Should().Be(GridLifecycle.PartiallyFilled);
    }

    [TestMethod]
    public async Task GivenPartiallyFilledGrid_WhenPositionOpenAndNoExitCondition_ThenReturnsEmptySignals()
    {
        var gridState = CreateGridState(GridLifecycle.PartiallyFilled, filledLevels: 2);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 99m),
            gridState,
            CreatePositionState(size: 2m, averageEntryPrice: 99.5m),
            DefaultConfigJson);

        signals.Should().BeEmpty();
        gridState.Lifecycle.Should().Be(GridLifecycle.PartiallyFilled);
    }

    [TestMethod]
    public async Task GivenPartiallyFilledGrid_WhenCandleCloseReachesTakeProfit_ThenEmitsMarketExitAndClosing()
    {
        var gridState = CreateGridState(GridLifecycle.PartiallyFilled, filledLevels: 2);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 101m),
            gridState,
            CreatePositionState(size: 2m, averageEntryPrice: 99.5m),
            DefaultConfigJson);

        signals.Should().ContainSingle();
        var signal = signals[0];
        signal.SignalType.Should().Be("TakeProfit");
        signal.Reason.Should().Be("Take profit triggered (partial fill).");
        signal.Parameters.Should().NotBeNull();
        signal.Parameters!["orderType"].Should().Be(OrderType.Market.ToString());
        signal.Parameters["targetPrice"].Should().Be(101m);
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenPartiallyFilledGrid_WhenStopLossTriggered_ThenEmitsMarketExitAndClosing()
    {
        var gridState = CreateGridState(GridLifecycle.PartiallyFilled, filledLevels: 2);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 94m),
            gridState,
            CreatePositionState(size: 2m, averageEntryPrice: 100m),
            DefaultConfigJson);

        signals.Should().ContainSingle();
        var signal = signals[0];
        signal.Reason.Should().Be("Stop loss triggered.");
        signal.Parameters.Should().NotBeNull();
        signal.Parameters!["orderType"].Should().Be(OrderType.Market.ToString());
        signal.Parameters["targetPrice"].Should().Be(94m);
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenFullyFilledGrid_WhenPositionOpen_ThenEmitsLimitTakeProfitAndClosing()
    {
        var gridState = CreateGridState(GridLifecycle.FullyFilled, filledLevels: 5);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 99.5m),
            gridState,
            CreatePositionState(size: 5m, averageEntryPrice: 99m),
            DefaultConfigJson);

        signals.Should().ContainSingle();
        var signal = signals[0];
        signal.SignalType.Should().Be("TakeProfit");
        signal.Reason.Should().Be("Take profit active.");
        signal.Parameters.Should().NotBeNull();
        signal.Parameters!["orderType"].Should().Be(OrderType.Limit.ToString());
        signal.Parameters["targetPrice"].Should().Be(99.99m);
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenFullyFilledGrid_WhenStopLossTriggered_ThenEmitsMarketExitInsteadOfLimitTakeProfit()
    {
        var gridState = CreateGridState(GridLifecycle.FullyFilled, filledLevels: 5);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 94m),
            gridState,
            CreatePositionState(size: 5m, averageEntryPrice: 100m),
            DefaultConfigJson);

        signals.Should().ContainSingle();
        signals[0].Parameters!["orderType"].Should().Be(OrderType.Market.ToString());
        signals[0].Reason.Should().Be("Stop loss triggered.");
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenClosingLifecycle_WhenPositionStillOpenAndNoStopLoss_ThenReturnsEmptySignals()
    {
        var gridState = CreateGridState(GridLifecycle.Closing, filledLevels: 5);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m),
            gridState,
            CreatePositionState(size: 5m, averageEntryPrice: 99m),
            DefaultConfigJson);

        signals.Should().BeEmpty();
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenClosingLifecycle_WhenStopLossTriggered_ThenEmitsMarketExit()
    {
        var gridState = CreateGridState(GridLifecycle.Closing, filledLevels: 5);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 94m),
            gridState,
            CreatePositionState(size: 5m, averageEntryPrice: 100m),
            DefaultConfigJson);

        signals.Should().ContainSingle();
        signals[0].Parameters!["orderType"].Should().Be(OrderType.Market.ToString());
        signals[0].Reason.Should().Be("Stop loss triggered.");
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenActiveLifecycle_WhenPositionOpen_ThenUsesFallbackLimitTakeProfit()
    {
        var gridState = CreateGridState(GridLifecycle.Active, filledLevels: 1);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m),
            gridState,
            CreatePositionState(size: 1m, averageEntryPrice: 99m),
            DefaultConfigJson);

        signals.Should().ContainSingle();
        var signal = signals[0];
        signal.Parameters.Should().NotBeNull();
        signal.Parameters!["orderType"].Should().Be(OrderType.Limit.ToString());
        signal.Parameters["targetPrice"].Should().Be(99.99m);
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenDeployingLifecycle_WhenPositionOpen_ThenUsesFallbackLimitTakeProfit()
    {
        var gridState = CreateGridState(GridLifecycle.Deploying, filledLevels: 1);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m),
            gridState,
            CreatePositionState(size: 1m, averageEntryPrice: 99m),
            DefaultConfigJson);

        signals.Should().ContainSingle();
        var signal = signals[0];
        signal.Parameters.Should().NotBeNull();
        signal.Parameters!["orderType"].Should().Be(OrderType.Limit.ToString());
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    private static StrategyEvaluation CreateEvaluation(bool setupDetected = true)
    {
        return new StrategyEvaluation
        {
            SetupDetected = setupDetected,
            Reason = "Test setup"
        };
    }

    private static GridState CreateGridState(
        GridLifecycle lifecycle,
        int filledLevels = 0,
        int totalLevels = 5,
        string? gridCycleId = "test-cycle-001")
    {
        return new GridState
        {
            Lifecycle = lifecycle,
            GridCycleId = gridCycleId,
            FilledLevels = filledLevels,
            TotalLevels = totalLevels
        };
    }

    private static PositionState CreatePositionState(decimal size, decimal averageEntryPrice)
    {
        return new PositionState
        {
            Symbol = "BTC",
            Size = size,
            AverageEntryPrice = averageEntryPrice,
            UnrealisedPnL = 0m
        };
    }

    private static MarketContext CreateMarketContext(decimal close)
    {
        return new MarketContext
        {
            Symbol = "BTC",
            TimestampUtc = CandleTimestamp,
            CurrentCandle = Candle.Create(
                "Binance",
                "BTC",
                "15m",
                CandleTimestamp,
                close,
                close + 1m,
                close - 1m,
                close,
                1_000m,
                10),
            Indicators = new IndicatorSnapshot()
        };
    }
}