using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;

namespace TradingApp.Application.Tests.Backtesting.Services;

[TestClass]
public sealed class BacktestAuditCollectorTests
{
    private BacktestAuditCollector _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new BacktestAuditCollector();
    }

    [TestMethod]
    public void GivenCandleEntry_WhenLogCandleEvaluation_ThenEntryIsStored()
    {
        var entry = CreateCandleEntry();

        _sut.LogCandleEvaluation(entry);

        _sut.CandleEvaluations.Should().ContainSingle().Which.Should().Be(entry);
    }

    [TestMethod]
    public void GivenOrderEventEntry_WhenLogOrderEvent_ThenEntryIsStored()
    {
        var entry = CreateOrderEventEntry();

        _sut.LogOrderEvent(entry);

        _sut.OrderEvents.Should().ContainSingle().Which.Should().Be(entry);
    }

    [TestMethod]
    public void GivenGridCycleEntry_WhenLogGridCycleCompleted_ThenEntryIsStored()
    {
        var entry = CreateGridCycleEntry();

        _sut.LogGridCycleCompleted(entry);

        _sut.GridCycles.Should().ContainSingle().Which.Should().Be(entry);
    }

    [TestMethod]
    public void GivenMultipleEntries_WhenLogged_ThenAllEntriesArePreservedInOrder()
    {
        var entry1 = CreateCandleEntry(timestampUtc: 1000);
        var entry2 = CreateCandleEntry(timestampUtc: 2000);

        _sut.LogCandleEvaluation(entry1);
        _sut.LogCandleEvaluation(entry2);

        _sut.CandleEvaluations.Should().HaveCount(2);
        _sut.CandleEvaluations[0].TimestampUtc.Should().Be(1000);
        _sut.CandleEvaluations[1].TimestampUtc.Should().Be(2000);
    }

    [TestMethod]
    public void GivenNullEntry_WhenLogCandleEvaluation_ThenThrowsArgumentNullException()
    {
        var act = () => _sut.LogCandleEvaluation(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void GivenNullEntry_WhenLogOrderEvent_ThenThrowsArgumentNullException()
    {
        var act = () => _sut.LogOrderEvent(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void GivenNullEntry_WhenLogGridCycleCompleted_ThenThrowsArgumentNullException()
    {
        var act = () => _sut.LogGridCycleCompleted(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void GivenNullCollector_WhenLogCandleEvaluation_ThenDoesNotThrow()
    {
        var act = () => NullBacktestAuditCollector.Instance.LogCandleEvaluation(CreateCandleEntry());

        act.Should().NotThrow();
    }

    [TestMethod]
    public void GivenNullCollector_WhenLogOrderEvent_ThenDoesNotThrow()
    {
        var act = () => NullBacktestAuditCollector.Instance.LogOrderEvent(CreateOrderEventEntry());

        act.Should().NotThrow();
    }

    [TestMethod]
    public void GivenNullCollector_WhenLogGridCycleCompleted_ThenDoesNotThrow()
    {
        var act = () => NullBacktestAuditCollector.Instance.LogGridCycleCompleted(CreateGridCycleEntry());

        act.Should().NotThrow();
    }

    private static CandleEvaluationEntry CreateCandleEntry(long timestampUtc = 1000)
    {
        return new CandleEvaluationEntry
        {
            TimestampUtc = timestampUtc,
            Open = 100m,
            High = 105m,
            Low = 95m,
            Close = 102m,
            Volume = 500m,
            IsWarmup = false,
            EmaFast = 101m,
            EmaSlow = 100m,
            EmaTrend = 99m,
            Rsi = 55m,
            Atr = 2.5m,
            SetupDetected = true,
            GridLifecycleState = "Active",
            PositionSize = 0.5m,
            PositionAvgEntry = 100m,
            SignalsEmitted = [],
            GridCycleId = "abc123"
        };
    }

    private static OrderEventEntry CreateOrderEventEntry()
    {
        return new OrderEventEntry
        {
            TimestampUtc = 1000,
            EventType = OrderEventType.Placed,
            OrderId = "order-1",
            Side = "Buy",
            OrderType = "Limit",
            Price = 100m,
            Size = 0.1m,
            GridCycleId = "abc123"
        };
    }

    private static GridCycleEntry CreateGridCycleEntry()
    {
        return new GridCycleEntry
        {
            GridCycleId = "abc123",
            DeployTimestampUtc = 1000,
            AnchorPrice = 100m,
            LevelsPlaced = 5,
            LevelPrices = [99m, 98m, 97m, 96m, 95m],
            LevelsFilled = 2,
            TakeProfitPrice = 102m,
            StopLossPrice = 94m,
            ExitReason = "TakeProfit",
            CyclePnl = 5.5m,
            CycleDurationMs = 3_600_000,
            CloseTimestampUtc = 4_600_000
        };
    }
}