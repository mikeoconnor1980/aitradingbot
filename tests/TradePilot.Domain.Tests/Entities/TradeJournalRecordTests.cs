using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Tests.Entities;

[TestClass]
public sealed class TradeJournalRecordTests
{
    private static readonly DateTime EntryTime = new(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void GivenScaleInAndScaleOutFills_WhenTradeCloses_ThenWeightedPricesCostsPnlAndDurationAreDeterministic()
    {
        var entryEvaluationId = Guid.NewGuid();
        var exitEvaluationId = Guid.NewGuid();
        var trade = CreateTrade(100m, 2m, 1m, entryEvaluationId);

        trade.AddEntryFill(EntryTime.AddMinutes(5), 110m, 1m, 0.5m);
        trade.AddExitFill(EntryTime.AddHours(1), 120m, 1m, 20m, 0.75m, exitEvaluationId, TradeExitReason.TakeProfit);
        trade.Status.Should().Be(TradeLifecycleStatus.PartiallyClosed);
        trade.AddExitFill(EntryTime.AddHours(2), 130m, 2m, 40m, 1.25m, exitEvaluationId, TradeExitReason.TakeProfit);

        trade.EntryPrice.Should().BeApproximately(103.33333333333333333333333333m, 0.00000001m);
        trade.ExitPrice.Should().BeApproximately(126.66666666666666666666666667m, 0.00000001m);
        trade.EntryQuantity.Should().Be(3m);
        trade.ExitQuantity.Should().Be(3m);
        trade.GrossPnl.Should().Be(60m);
        trade.Fees.Should().Be(3.5m);
        trade.Funding.Should().BeNull();
        trade.NetPnl.Should().Be(56.5m);
        trade.DurationMilliseconds.Should().Be((long)TimeSpan.FromHours(2).TotalMilliseconds);
        trade.EntryStrategyEvaluationId.Should().Be(entryEvaluationId);
        trade.ExitStrategyEvaluationId.Should().Be(exitEvaluationId);
        trade.Status.Should().Be(TradeLifecycleStatus.Closed);
    }

    [TestMethod]
    public void GivenClosedTrade_WhenLaterFillAttemptsMutation_ThenHistoricalEvidenceIsImmutable()
    {
        var trade = CreateTrade(100m, 1m, 0m, Guid.NewGuid());
        trade.AddExitFill(EntryTime.AddMinutes(30), 90m, 1m, -10m, 1m, null, TradeExitReason.StopLoss);

        var action = () => trade.AddEntryFill(EntryTime.AddMinutes(40), 80m, 1m, 1m);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*immutable*");
        trade.NetPnl.Should().Be(-11m);
    }

    [TestMethod]
    public void GivenInvalidLifecycleEvidence_WhenApplied_ThenExplicitReconciliationIsRequired()
    {
        var trade = CreateTrade(100m, 1m, 0m, Guid.NewGuid());

        var earlyExit = () => trade.AddExitFill(
            EntryTime.AddSeconds(-1), 100m, 1m, 0m, 0m, null, TradeExitReason.External);
        var overClose = () => trade.AddExitFill(
            EntryTime.AddMinutes(1), 100m, 2m, 0m, 0m, null, TradeExitReason.External);

        earlyExit.Should().Throw<InvalidOperationException>().WithMessage("*precede*");
        overClose.Should().Throw<InvalidOperationException>().WithMessage("*remaining quantity*");
        trade.Status.Should().Be(TradeLifecycleStatus.Open);
    }

    [TestMethod]
    public void GivenFinalizedExcursions_WhenCorrectedSilently_ThenHistoricalEvidenceIsImmutable()
    {
        var trade = CreateTrade(100m, 1m, 0m, Guid.NewGuid());
        trade.AddExitFill(EntryTime.AddMinutes(30), 110m, 1m, 10m, 0m, null, TradeExitReason.TakeProfit);
        trade.SetExcursions(20m, 20m, -5m, -5m);

        var correction = () => trade.SetExcursions(25m, 25m, -5m, -5m);

        correction.Should().Throw<InvalidOperationException>().WithMessage("*immutable*");
        trade.MfeAmount.Should().Be(20m);
    }

    private static TradeJournalRecord CreateTrade(
        decimal price,
        decimal quantity,
        decimal fee,
        Guid entryEvaluationId)
    {
        return TradeJournalRecord.Open(
            "user-1",
            Guid.NewGuid(),
            "v10.4",
            4,
            new string('a', 64),
            "BTC",
            TradeSide.Long,
            EntryTime,
            price,
            quantity,
            fee,
            5m,
            entryEvaluationId,
            "Bullish",
            "15m",
            "Hyperliquid",
            "cycle-1");
    }
}
