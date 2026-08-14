using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TradePilot.Application.TradeJournal.Models;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Persistence.Repositories;

namespace TradePilot.Persistence.Tests.Repositories;

[TestClass]
public sealed class TradeJournalRepositoryTests
{
    private static readonly Guid StrategyId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private DbContextOptions<TradePilotDbContext> _contextOptions = null!;

    [TestInitialize]
    public void Setup()
    {
        _contextOptions = new DbContextOptionsBuilder<TradePilotDbContext>()
            .UseInMemoryDatabase($"TradeJournalTests-{Guid.NewGuid():N}")
            .Options;
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    [TestMethod]
    public async Task GivenJournalHistory_WhenQueried_ThenOwnershipStrategyVersionSymbolTimeOutcomeOrderingAndLimitApply()
    {
        var first = CreateClosedTrade("user-1", "BTC", 4, "Bullish", 10m, 1m, 0m);
        var second = CreateClosedTrade("user-1", "BTC", 4, "Bullish", -5m, 1m, 1m, hoursAfterFirst: 2);
        var otherVersion = CreateClosedTrade("user-1", "BTC", 5, "Bearish", -20m, 1m, 1m, hoursAfterFirst: 4);
        var otherUser = CreateClosedTrade("user-2", "BTC", 4, "Bullish", -50m, 1m, 1m, hoursAfterFirst: 6);
        await AddAsync(first, second, otherVersion, otherUser);

        await using var context = CreateContext();
        var result = await CreateRepository(context).GetAsync(new TradeJournalFilter(
            "user-1",
            StrategyId,
            StrategyVersion: 4,
            Symbol: "btc",
            FromUtc: first.ExitTimeUtc,
            ToUtc: second.ExitTimeUtc,
            Outcome: TradeOutcome.Loser), 1);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(second.Id);
    }

    [TestMethod]
    public async Task GivenKnownTrades_WhenAnalyticsRequested_ThenAllMetricsAreDatabaseCalculated()
    {
        await AddAsync(
            CreateClosedTrade("user-1", "BTC", 4, "Bullish", 20m, 1m, 1m, mfe: 30m, mae: -5m, durationHours: 1),
            CreateClosedTrade("user-1", "BTC", 4, "Bullish", -10m, 1m, 1m, mfe: 5m, mae: -15m, durationHours: 3),
            CreateClosedTrade("user-1", "BTC", 4, "Bullish", 0m, 0m, 0m, mfe: 10m, mae: -10m, durationHours: 2));

        await using var context = CreateContext();
        var result = await CreateRepository(context).GetAnalyticsAsync(new TradeJournalFilter("user-1"));

        result.TradeCount.Should().Be(3);
        result.WinningTrades.Should().Be(1);
        result.LosingTrades.Should().Be(1);
        result.BreakevenTrades.Should().Be(1);
        result.GrossPnl.Should().Be(10m);
        result.NetPnl.Should().Be(6m);
        result.Fees.Should().Be(4m);
        result.Funding.Should().BeNull();
        result.FundingComplete.Should().BeFalse();
        result.WinRate.Should().BeApproximately(33.3333m, 0.0001m);
        result.AverageWin.Should().Be(18m);
        result.AverageLoss.Should().Be(-12m);
        result.AverageNetPnlPerTrade.Should().Be(2m);
        result.ProfitFactor.Should().Be(1.5m);
        result.AverageDuration.Should().Be(TimeSpan.FromHours(2));
        result.AverageMfeAmount.Should().Be(15m);
        result.AverageMaeAmount.Should().Be(-10m);
        result.BestTrade!.NetPnl.Should().Be(18m);
        result.WorstTrade!.NetPnl.Should().Be(-12m);
    }

    [TestMethod]
    public async Task GivenAllWinnersAndVersionRegimes_WhenGrouped_ThenZeroLossContractAndGroupsAreExplicit()
    {
        await AddAsync(
            CreateClosedTrade("user-1", "BTC", 4, "Bullish", 10m, 1m, 0m),
            CreateClosedTrade("user-1", "BTC", 5, "Bearish", 20m, 1m, 0m, hoursAfterFirst: 2));

        await using var context = CreateContext();
        var repository = CreateRepository(context);
        var analytics = await repository.GetAnalyticsAsync(new TradeJournalFilter("user-1"));
        var grouped = await repository.GetStrategyAnalyticsAsync(new TradeJournalFilter("user-1", StrategyId));

        analytics.ProfitFactor.Should().BeNull();
        analytics.ProfitFactorHasZeroLossDenominator.Should().BeTrue();
        grouped.ByStrategyVersion.Select(group => group.Key).Should().Equal("4", "5");
        grouped.ByEntryMarketRegime.Select(group => group.Key).Should().Equal("Bearish", "Bullish");
        grouped.ByStrategyVersion.Sum(group => group.Analytics.TradeCount).Should().Be(2);
    }

    private async Task AddAsync(params TradeJournalRecord[] trades)
    {
        await using var context = CreateContext();
        context.TradeJournalRecords.AddRange(trades);
        await context.SaveChangesAsync();
    }

    private TradePilotDbContext CreateContext() => new(_contextOptions);

    private static TradeJournalRepository CreateRepository(TradePilotDbContext context)
    {
        return new TradeJournalRepository(context, NullLogger<TradeJournalRepository>.Instance);
    }

    private static TradeJournalRecord CreateClosedTrade(
        string userId,
        string symbol,
        int version,
        string regime,
        decimal grossPnl,
        decimal entryFee,
        decimal exitFee,
        decimal mfe = 10m,
        decimal mae = -5m,
        int durationHours = 1,
        int hoursAfterFirst = 0)
    {
        var entryTime = new DateTime(2026, 8, 10, 10, 0, 0, DateTimeKind.Utc).AddHours(hoursAfterFirst);
        var trade = TradeJournalRecord.Open(
            userId,
            StrategyId,
            "v10",
            version,
            new string((char)('a' + version), 64),
            symbol,
            TradeSide.Long,
            entryTime,
            100m,
            1m,
            entryFee,
            5m,
            null,
            regime,
            "15m",
            "Hyperliquid",
            Guid.NewGuid().ToString("N"));
        trade.AddExitFill(
            entryTime.AddHours(durationHours),
            100m + grossPnl,
            1m,
            grossPnl,
            exitFee,
            null,
            grossPnl >= 0m ? TradeExitReason.TakeProfit : TradeExitReason.StopLoss);
        trade.SetExcursions(mfe, mfe, mae, mae);
        return trade;
    }
}
