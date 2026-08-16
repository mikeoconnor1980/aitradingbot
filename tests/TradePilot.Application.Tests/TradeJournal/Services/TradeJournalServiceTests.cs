using Microsoft.Extensions.Logging.Abstractions;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.TradeJournal.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.TradeJournal.Services;

[TestClass]
public sealed class TradeJournalServiceTests
{
    [TestMethod]
    public async Task GivenOpeningAndClosingFills_WhenProjected_ThenEvaluationVersionRegimeAndExcursionsArePreserved()
    {
        TradeJournalRecord? current = null;
        var repository = new Mock<ITradeJournalRepository>();
        repository.Setup(value => value.GetOpenAsync("user-1", "BTC", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => current);
        repository.Setup(value => value.AddAsync(It.IsAny<TradeJournalRecord>(), It.IsAny<CancellationToken>()))
            .Callback<TradeJournalRecord, CancellationToken>((trade, _) => current = trade)
            .Returns(Task.CompletedTask);
        repository.Setup(value => value.UpdateAsync(It.IsAny<TradeJournalRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var candles = new Mock<ICandleRepository>();
        candles.Setup(value => value.GetCandlesAsync(
                "BTC", "15m", It.IsAny<long>(), It.IsAny<long>(), "Hyperliquid", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Candle.Create("Hyperliquid", "BTC", "15m", 1_000, 100m, 125m, 90m, 120m, 1m, 1),
            ]);
        var sut = new TradeJournalService(
            repository.Object,
            candles.Object,
            NullLogger<TradeJournalService>.Instance);
        var strategyId = Guid.NewGuid();
        var entryEvaluationId = Guid.NewGuid();
        var exitEvaluationId = Guid.NewGuid();
        var entryEvidence = Evidence(strategyId, entryEvaluationId, null);
        var exitEvidence = Evidence(strategyId, exitEvaluationId, TradeExitReason.TakeProfit);

        await sut.RecordFillAsync(Fill("entry", 100m, 2m, 1m, 0m), entryEvidence, false);
        await sut.RecordFillAsync(Fill("exit", 120m, 2m, 2m, 40m, minutes: 30), exitEvidence, true);

        current.Should().NotBeNull();
        current!.Status.Should().Be(TradeLifecycleStatus.Closed);
        current.StrategyId.Should().Be(strategyId);
        current.StrategyVersion.Should().Be(4);
        current.EntryStrategyEvaluationId.Should().Be(entryEvaluationId);
        current.ExitStrategyEvaluationId.Should().Be(exitEvaluationId);
        current.EntryMarketRegime.Should().Be("Bullish");
        current.GrossPnl.Should().Be(40m);
        current.Fees.Should().Be(3m);
        current.NetPnl.Should().Be(37m);
        current.MfeAmount.Should().Be(50m);
        current.MaeAmount.Should().Be(-20m);
        current.Funding.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenUnmatchedCloseFill_WhenProjected_ThenNoHistoricalTradeIsFabricated()
    {
        var repository = new Mock<ITradeJournalRepository>();
        repository.Setup(value => value.GetOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradeJournalRecord?)null);
        var sut = new TradeJournalService(
            repository.Object,
            Mock.Of<ICandleRepository>(),
            NullLogger<TradeJournalService>.Instance);

        await sut.RecordFillAsync(Fill("exit", 90m, 1m, 1m, -10m), Evidence(Guid.NewGuid(), Guid.NewGuid(), TradeExitReason.External), true);

        repository.Verify(value => value.AddAsync(It.IsAny<TradeJournalRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(value => value.UpdateAsync(It.IsAny<TradeJournalRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenNoPersistedCandles_WhenTradeCloses_ThenExcursionsRemainExplicitlyUnavailable()
    {
        TradeJournalRecord? current = null;
        var repository = new Mock<ITradeJournalRepository>();
        repository.Setup(value => value.GetOpenAsync("user-1", "BTC", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => current);
        repository.Setup(value => value.AddAsync(It.IsAny<TradeJournalRecord>(), It.IsAny<CancellationToken>()))
            .Callback<TradeJournalRecord, CancellationToken>((trade, _) => current = trade)
            .Returns(Task.CompletedTask);
        repository.Setup(value => value.UpdateAsync(It.IsAny<TradeJournalRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var candles = new Mock<ICandleRepository>();
        candles.Setup(value => value.GetCandlesAsync(
                "BTC", "15m", It.IsAny<long>(), It.IsAny<long>(), "Hyperliquid", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var sut = new TradeJournalService(
            repository.Object,
            candles.Object,
            NullLogger<TradeJournalService>.Instance);
        var strategyId = Guid.NewGuid();

        await sut.RecordFillAsync(Fill("entry", 100m, 1m, 1m, 0m), Evidence(strategyId, Guid.NewGuid(), null), false);
        await sut.RecordFillAsync(Fill("exit", 110m, 1m, 1m, 10m, minutes: 30), Evidence(strategyId, Guid.NewGuid(), TradeExitReason.TakeProfit), true);

        current!.Status.Should().Be(TradeLifecycleStatus.Closed);
        current.MfeAmount.Should().BeNull();
        current.MaeAmount.Should().BeNull();
    }

    private static TradeExecutionEvidence Evidence(
        Guid strategyId,
        Guid evaluationId,
        TradeExitReason? exitReason)
    {
        return new TradeExecutionEvidence(
            strategyId,
            "v10.4",
            4,
            new string('a', 64),
            evaluationId,
            "Bullish",
            "15m",
            TradeSide.Long,
            5m,
            "Hyperliquid",
            exitReason);
    }

    private static LiveFill Fill(
        string orderId,
        decimal price,
        decimal size,
        decimal fee,
        decimal closedPnl,
        int minutes = 0)
    {
        return new LiveFill
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Symbol = "BTC",
            Side = orderId == "entry" ? OrderSide.Buy : OrderSide.Sell,
            Direction = orderId == "entry" ? "Open Long" : "Close Long",
            Price = price,
            Size = size,
            Fee = fee,
            ClosedPnl = closedPnl,
            FilledAtUtc = new DateTime(2026, 8, 14, 10, minutes, 0, DateTimeKind.Utc),
            UserId = "user-1",
            GridCycleId = "cycle-1",
            TradeType = orderId == "entry" ? TradeType.SignalEntry.ToString() : TradeType.TakeProfit.ToString(),
        };
    }
}
