using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.TradeJournal.Models;
using TradePilot.Application.TradeJournal.Queries;

namespace TradePilot.Application.Tests.TradeJournal.Queries;

[TestClass]
public sealed class TradeJournalQueryHandlerTests
{
    [TestMethod]
    public async Task GivenHistoryRequest_WhenHandled_ThenFiltersAndBoundedLimitDelegateExactly()
    {
        var repository = new Mock<ITradeJournalRepository>();
        repository.Setup(value => value.GetAsync(It.IsAny<TradeJournalFilter>(), 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var from = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);
        var handler = new GetTradesQueryHandler(repository.Object);

        var result = await handler.Handle(new GetTradesQuery(
            "user-1",
            StrategyVersion: 4,
            Symbol: "BTC",
            Side: TradeSide.Long,
            From: from,
            Outcome: TradeOutcome.Loser,
            Limit: 900), CancellationToken.None);

        result.Limit.Should().Be(500);
        repository.Verify(value => value.GetAsync(
            It.Is<TradeJournalFilter>(filter =>
                filter.UserId == "user-1"
                && filter.StrategyVersion == 4
                && filter.Symbol == "BTC"
                && filter.Side == TradeSide.Long
                && filter.FromUtc == from.UtcDateTime
                && filter.Outcome == TradeOutcome.Loser),
            500,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenAnalyticsRequest_WhenHandled_ThenPersistenceCalculatesStatistics()
    {
        var expected = new TradeAnalytics(
            2, 1, 1, 0, 20m, 16m, 4m, null, false, 50m, 20m, -4m, 8m,
            5m, false, TimeSpan.FromHours(2), 30m, 10m, -12m, -4m, null, null);
        var repository = new Mock<ITradeJournalRepository>();
        repository.Setup(value => value.GetAnalyticsAsync(It.IsAny<TradeJournalFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var handler = new GetTradeAnalyticsQueryHandler(repository.Object);

        var result = await handler.Handle(new GetTradeAnalyticsQuery("user-1", Symbol: "BTC"), CancellationToken.None);

        result.Should().BeSameAs(expected);
        repository.Verify(value => value.GetAnalyticsAsync(
            It.Is<TradeJournalFilter>(filter => filter.UserId == "user-1" && filter.Symbol == "BTC"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
