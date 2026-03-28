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
            strategyConfigJson: "{\"gridLevels\":10}",
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
            tradesJson: "[]");

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
        result.StrategyConfigJson.Should().Be("{\"gridLevels\":10}");
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
        result.TradesJson.Should().Be("[]");
        result.CreatedAtUtc.Should().BePositive();
    }

    [TestMethod]
    public async Task GivenNonExistentId_WhenGetByIdAsync_ThenReturnsNull()
    {
        await using var context = CreateContext();
        var sut = new BacktestRunRepository(context);

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}