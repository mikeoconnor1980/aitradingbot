using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TradingApp.Domain.Entities;
using TradingApp.Persistence.Repositories;

namespace TradingApp.Persistence.Tests.Repositories;

[TestClass]
public sealed class BacktestRunRepositoryTests
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<TradingAppDbContext> _contextOptions = null!;

    private const string StrategyConfigJson = """{"schemaVersion":1,"strategyMode":"grid","strategyName":"Test","exchange":"Hyperliquid","market":"BTC-USD","timeframe":"15m","direction":"long","enabled":true,"grid":{"levels":10,"spacing":0.5,"entryMode":"AutoFromSignalCandle","breakdownThreshold":1.5},"exit":{"takeProfit":{"enabled":true,"type":"fixed_percent","value":2},"stopLoss":{"enabled":true,"type":"fixed_percent","value":6}},"risk":{"positionSizeType":"percent_wallet","positionSizeValue":5,"leverage":1,"maxOpenTrades":1}}""";
    private const string QueuedStrategyConfigJson = """{"schemaVersion":1,"strategyMode":"grid","strategyName":"Queued","exchange":"Hyperliquid","market":"BTC-USD","timeframe":"15m","direction":"long","enabled":true,"grid":{"levels":5,"spacing":0.5,"entryMode":"AutoFromSignalCandle","breakdownThreshold":1.5},"exit":{"takeProfit":{"enabled":true,"type":"fixed_percent","value":2},"stopLoss":{"enabled":true,"type":"fixed_percent","value":6}},"risk":{"positionSizeType":"percent_wallet","positionSizeValue":5,"leverage":1,"maxOpenTrades":1}}""";
    private const string ExecutionConfigJson = """{"feeModel":{"makerFeeRate":0.0001,"takerFeeRate":0.00035,"slippageRate":0}}""";

    [TestInitialize]
    public void Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<TradingAppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new TradingAppDbContext(_contextOptions);
        context.Database.EnsureCreated();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _connection.Dispose();
    }

    private TradingAppDbContext CreateContext() => new(_contextOptions);

    [TestMethod]
    public async Task GivenBacktestRun_WhenAddAsync_ThenCanBeRetrievedById()
    {
        var backtestRun = BacktestRun.Create(
            symbol: "BTC",
            intervalsJson: "[\"15m\",\"1h\",\"4h\"]",
            startDateUtc: 1704067200000,
            endDateUtc: 1735689599000,
            strategyConfigJson: StrategyConfigJson,
            executionConfigJson: ExecutionConfigJson,
            initialCapital: 10000m,
            candlesReplayed: 35040,
            elapsedMs: 12500,
            totalTrades: 847,
            winningTrades: 612,
            losingTrades: 235,
            winRate: 72.3m,
            totalPnl: 4521.87m,
            maxDrawdown: -1234.56m,
            averageTradePnl: 5.34m,
            averageHoldTimeMinutes: 245.0,
            hedgesOpened: 12,
            totalFeesPaid: 89.23m,
            tradesJson: "[]",
            expectancy: 0.56m,
            profitFactor: 2.1667m,
            sqn: 1.4321m);

        await using (var writeContext = CreateContext())
        {
            var sut = new BacktestRunRepository(writeContext);
            await sut.AddAsync(backtestRun);
        }

        await using var readContext = CreateContext();
        var readSut = new BacktestRunRepository(readContext);
        var result = await readSut.GetByIdAsync(backtestRun.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(backtestRun.Id);
        result.Symbol.Should().Be("BTC");
        result.IntervalsJson.Should().Be("[\"15m\",\"1h\",\"4h\"]");
        result.StartDateUtc.Should().Be(1704067200000);
        result.EndDateUtc.Should().Be(1735689599000);
        result.StrategyConfigJson.Should().Be(StrategyConfigJson);
        result.ExecutionConfigJson.Should().Be(ExecutionConfigJson);
        result.InitialCapital.Should().Be(10000m);
        result.CandlesReplayed.Should().Be(35040);
        result.ElapsedMs.Should().Be(12500);
        result.TotalTrades.Should().Be(847);
        result.WinningTrades.Should().Be(612);
        result.LosingTrades.Should().Be(235);
        result.WinRate.Should().BeApproximately(72.3m, 0.01m);
        result.TotalPnl.Should().BeApproximately(4521.87m, 0.01m);
        result.MaxDrawdown.Should().BeApproximately(-1234.56m, 0.01m);
        result.AverageTradePnl.Should().BeApproximately(5.34m, 0.01m);
        result.AverageHoldTimeMinutes.Should().BeApproximately(245.0, 0.001);
        result.HedgesOpened.Should().Be(12);
        result.TotalFeesPaid.Should().BeApproximately(89.23m, 0.01m);
        result.Expectancy.Should().BeApproximately(0.56m, 0.0001m);
        result.ProfitFactor.Should().BeApproximately(2.1667m, 0.0001m);
        result.Sqn.Should().BeApproximately(1.4321m, 0.0001m);
        result.TradesJson.Should().Be("[]");
        result.CreatedAtUtc.Should().BePositive();
    }

    [TestMethod]
    public async Task GivenBacktestRunWithAuditLog_WhenPersisted_ThenDebugDataIsRetrievable()
    {
        var backtestRun = BacktestRun.CreateQueued(
            symbol: "BTC",
            intervalsJson: "[\"15m\",\"1h\",\"4h\"]",
            startDateUtc: 1000,
            endDateUtc: 2000,
            strategyConfigJson: QueuedStrategyConfigJson,
            executionConfigJson: ExecutionConfigJson,
            initialCapital: 10000m,
            auditLogEnabled: true);

        backtestRun.MarkRunning(100);
        backtestRun.MarkCompleted(
            candlesReplayed: 100,
            elapsedMs: 5000,
            totalTrades: 5,
            winningTrades: 3,
            losingTrades: 2,
            winRate: 0.6m,
            totalPnl: 50m,
            maxDrawdown: 10m,
            averageTradePnl: 10m,
            averageHoldTimeMinutes: 60,
            hedgesOpened: 0,
            totalFeesPaid: 2m,
            tradesJson: "[]",
            equityTimeSeriesJson: "[]",
            candleLogJson: "[{\"timestampUtc\":1000}]",
            orderEventLogJson: "[{\"timestampUtc\":2000}]",
            gridCycleLogJson: "[{\"gridCycleId\":\"abc\"}]");

        await using (var writeContext = CreateContext())
        {
            var sut = new BacktestRunRepository(writeContext);
            await sut.AddAsync(backtestRun);
        }

        await using var readContext = CreateContext();
        var readSut = new BacktestRunRepository(readContext);
        var result = await readSut.GetByIdAsync(backtestRun.Id);

        result.Should().NotBeNull();
        result!.AuditLogEnabled.Should().BeTrue();
        result.CandleLogJson.Should().Contain("timestampUtc");
        result.OrderEventLogJson.Should().Contain("timestampUtc");
        result.GridCycleLogJson.Should().Contain("gridCycleId");
    }

    [TestMethod]
    public async Task GivenBacktestRunWithoutAuditLog_WhenPersisted_ThenDebugColumnsAreNull()
    {
        var backtestRun = BacktestRun.CreateQueued(
            symbol: "BTC",
            intervalsJson: "[\"15m\",\"1h\",\"4h\"]",
            startDateUtc: 1000,
            endDateUtc: 2000,
            strategyConfigJson: QueuedStrategyConfigJson,
            executionConfigJson: ExecutionConfigJson,
            initialCapital: 10000m,
            auditLogEnabled: false);

        backtestRun.MarkRunning(100);
        backtestRun.MarkCompleted(
            candlesReplayed: 100,
            elapsedMs: 5000,
            totalTrades: 0,
            winningTrades: 0,
            losingTrades: 0,
            winRate: 0m,
            totalPnl: 0m,
            maxDrawdown: 0m,
            averageTradePnl: 0m,
            averageHoldTimeMinutes: 0,
            hedgesOpened: 0,
            totalFeesPaid: 0m,
            tradesJson: "[]",
            equityTimeSeriesJson: "[]");

        await using (var writeContext = CreateContext())
        {
            var sut = new BacktestRunRepository(writeContext);
            await sut.AddAsync(backtestRun);
        }

        await using var readContext = CreateContext();
        var readSut = new BacktestRunRepository(readContext);
        var result = await readSut.GetByIdAsync(backtestRun.Id);

        result.Should().NotBeNull();
        result!.AuditLogEnabled.Should().BeFalse();
        result.CandleLogJson.Should().BeNull();
        result.OrderEventLogJson.Should().BeNull();
        result.GridCycleLogJson.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenNonExistentId_WhenGetByIdAsync_ThenReturnsNull()
    {
        await using var context = CreateContext();
        var sut = new BacktestRunRepository(context);

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenRunsWithStrategyId_WhenGetPagedSummariesByStrategyAsync_ThenReturnsOnlyMatchingRuns()
    {
        var strategyId = Guid.NewGuid();
        var otherStrategyId = Guid.NewGuid();

        var matchingRunOne = BacktestRun.CreateQueued(
            symbol: "BTC",
            intervalsJson: "[\"15m\"]",
            startDateUtc: 1000,
            endDateUtc: 2000,
            strategyConfigJson: QueuedStrategyConfigJson,
            executionConfigJson: ExecutionConfigJson,
            initialCapital: 10000m,
            strategyId: strategyId,
            strategyRevisionId: 1);
        MarkAsCompleted(matchingRunOne);

        var matchingRunTwo = BacktestRun.CreateQueued(
            symbol: "ETH",
            intervalsJson: "[\"1h\"]",
            startDateUtc: 3000,
            endDateUtc: 4000,
            strategyConfigJson: QueuedStrategyConfigJson,
            executionConfigJson: ExecutionConfigJson,
            initialCapital: 5000m,
            strategyId: strategyId,
            strategyRevisionId: 2);
        MarkAsCompleted(matchingRunTwo);

        var otherRun = BacktestRun.CreateQueued(
            symbol: "SOL",
            intervalsJson: "[\"4h\"]",
            startDateUtc: 5000,
            endDateUtc: 6000,
            strategyConfigJson: QueuedStrategyConfigJson,
            executionConfigJson: ExecutionConfigJson,
            initialCapital: 2500m,
            strategyId: otherStrategyId,
            strategyRevisionId: 1);
        MarkAsCompleted(otherRun);

        await using (var writeContext = CreateContext())
        {
            writeContext.BacktestRuns.AddRange(matchingRunOne, matchingRunTwo, otherRun);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = CreateContext();
        var sut = new BacktestRunRepository(readContext);

        var result = await sut.GetPagedSummariesByStrategyAsync(strategyId, 1, 10);

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Select(item => item.StrategyId).Should().OnlyContain(id => id == strategyId);
        result.Items.Select(item => item.StrategyRevisionId).Should().BeEquivalentTo([2, 1]);
        result.Items.Select(item => item.StrategyName).Should().OnlyContain(name => name == null);
    }

    [TestMethod]
    public async Task GivenMultipleMatchingRuns_WhenGetPagedSummariesByStrategyAsyncWithSmallPageSize_ThenReturnsOnlyRequestedPage()
    {
        var strategyId = Guid.NewGuid();

        var matchingRunOne = BacktestRun.CreateQueued(
            symbol: "BTC",
            intervalsJson: "[\"15m\"]",
            startDateUtc: 1000,
            endDateUtc: 2000,
            strategyConfigJson: QueuedStrategyConfigJson,
            executionConfigJson: ExecutionConfigJson,
            initialCapital: 10000m,
            strategyId: strategyId,
            strategyRevisionId: 1);
        MarkAsCompleted(matchingRunOne);

        var matchingRunTwo = BacktestRun.CreateQueued(
            symbol: "ETH",
            intervalsJson: "[\"1h\"]",
            startDateUtc: 3000,
            endDateUtc: 4000,
            strategyConfigJson: QueuedStrategyConfigJson,
            executionConfigJson: ExecutionConfigJson,
            initialCapital: 5000m,
            strategyId: strategyId,
            strategyRevisionId: 2);
        MarkAsCompleted(matchingRunTwo);

        await using (var writeContext = CreateContext())
        {
            writeContext.BacktestRuns.AddRange(matchingRunOne, matchingRunTwo);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = CreateContext();
        var sut = new BacktestRunRepository(readContext);

        var result = await sut.GetPagedSummariesByStrategyAsync(strategyId, 1, 1);

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(1);
    }

    private static void MarkAsCompleted(BacktestRun run)
    {
        run.MarkCompleted(
            candlesReplayed: 100,
            elapsedMs: 500,
            totalTrades: 0,
            winningTrades: 0,
            losingTrades: 0,
            winRate: 0m,
            totalPnl: 0m,
            maxDrawdown: 0m,
            averageTradePnl: 0m,
            averageHoldTimeMinutes: 0,
            hedgesOpened: 0,
            totalFeesPaid: 0m,
            tradesJson: "[]",
            equityTimeSeriesJson: "[]");
    }
}