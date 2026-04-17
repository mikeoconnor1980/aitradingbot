using Microsoft.EntityFrameworkCore;
using TradePilot.Domain.Entities;
using TradePilot.Persistence.Repositories;

namespace TradePilot.Persistence.Tests.Repositories;

[TestClass]
public sealed class CandleRepositoryTests
{
    private DbContextOptions<TradePilotDbContext> _contextOptions = null!;

    [TestInitialize]
    public void Setup()
    {
        _contextOptions = new DbContextOptionsBuilder<TradePilotDbContext>()
            .UseInMemoryDatabase(databaseName: $"CandleTests-{Guid.NewGuid():N}")
            .Options;

        using var context = new TradePilotDbContext(_contextOptions);
        context.Database.EnsureCreated();
    }

    private TradePilotDbContext CreateContext() => new(_contextOptions);

    [TestMethod]
    public async Task GivenCandles_WhenBulkInsertAsync_ThenAllCandlesArePersisted()
    {
        var candles = CreateCandles("BTC", "15m", 1000, 3);
        await using var context = CreateContext();
        var sut = new CandleRepository(context);

        await sut.BulkInsertAsync(candles);

        await using var verifyContext = CreateContext();
        var stored = await verifyContext.Candles.ToListAsync();
        stored.Should().HaveCount(3);
        stored.Should().OnlyContain(c => c.Source == "Hyperliquid");
    }

    [TestMethod]
    public async Task GivenDuplicateCandles_WhenBulkInsertAsync_ThenDuplicatesAreSkipped()
    {
        var candles = CreateCandles("BTC", "15m", 1000, 3);
        await using var context1 = CreateContext();
        var sut1 = new CandleRepository(context1);
        await sut1.BulkInsertAsync(candles);

        var duplicatesWithNew = candles.Concat(CreateCandles("BTC", "15m", 4000, 1)).ToList();
        await using var context2 = CreateContext();
        var sut2 = new CandleRepository(context2);
        await sut2.BulkInsertAsync(duplicatesWithNew);

        await using var verifyContext = CreateContext();
        var stored = await verifyContext.Candles.ToListAsync();
        stored.Should().HaveCount(4);
    }

    [TestMethod]
    public async Task GivenCandlesWithSameTimestampButDifferentSources_WhenBulkInsertAsync_ThenBothCandlesArePersisted()
    {
        var candles = new[]
        {
            Candle.Create("BTC", "15m", 1000, 100m, 105m, 95m, 102m, 50m, 10, source: "Hyperliquid"),
            Candle.Create("BTC", "15m", 1000, 100m, 105m, 95m, 102m, 50m, 10, source: "Binance")
        };

        await using var context = CreateContext();
        var sut = new CandleRepository(context);

        await sut.BulkInsertAsync(candles);

        await using var verifyContext = CreateContext();
        var stored = await verifyContext.Candles
            .OrderBy(c => c.Source)
            .ToListAsync();

        stored.Should().HaveCount(2);
        stored.Select(c => c.Source).Should().Equal("Binance", "Hyperliquid");
    }

    [TestMethod]
    public async Task GivenCandlesInRange_WhenGetCandlesAsync_ThenReturnsFilteredOrderedByTimestamp()
    {
        var candles = new[]
        {
            Candle.Create("BTC", "15m", 3000, 100m, 105m, 95m, 102m, 50m, 10),
            Candle.Create("BTC", "15m", 1000, 100m, 105m, 95m, 102m, 50m, 10),
            Candle.Create("BTC", "15m", 2000, 100m, 105m, 95m, 102m, 50m, 10),
            Candle.Create("BTC", "15m", 4000, 100m, 105m, 95m, 102m, 50m, 10),
            Candle.Create("ETH", "15m", 1000, 100m, 105m, 95m, 102m, 50m, 10)
        };

        await using var context = CreateContext();
        var sut = new CandleRepository(context);
        await sut.BulkInsertAsync(candles);

        await using var queryContext = CreateContext();
        var querySut = new CandleRepository(queryContext);
        var result = await querySut.GetCandlesAsync("BTC", "15m", 1000, 3000);

        result.Should().HaveCount(3);
        result.Select(c => c.Timestamp).Should().BeInAscendingOrder();
        result.Should().OnlyContain(c => c.Symbol == "BTC");
    }

    [TestMethod]
    public async Task GivenSourceFilter_WhenGetCandlesAsync_ThenReturnsOnlyMatchingSource()
    {
        var candles = new[]
        {
            Candle.Create("BTC", "15m", 1000, 100m, 105m, 95m, 102m, 50m, 10, source: "Hyperliquid"),
            Candle.Create("BTC", "15m", 1000, 100m, 105m, 95m, 102m, 50m, 10, source: "Binance"),
            Candle.Create("BTC", "15m", 2000, 100m, 105m, 95m, 102m, 50m, 10, source: "Binance")
        };

        await using var context = CreateContext();
        var sut = new CandleRepository(context);
        await sut.BulkInsertAsync(candles);

        await using var queryContext = CreateContext();
        var querySut = new CandleRepository(queryContext);
        var result = await querySut.GetCandlesAsync("BTC", "15m", 1000, 2000, source: "Binance");

        result.Should().HaveCount(2);
        result.Should().OnlyContain(c => c.Source == "Binance");
    }

    [TestMethod]
    public async Task GivenNoCandlesInRange_WhenGetCandlesAsync_ThenReturnsEmptyList()
    {
        await using var context = CreateContext();
        var sut = new CandleRepository(context);

        var result = await sut.GetCandlesAsync("BTC", "15m", 1000, 2000);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenCandlesExist_WhenGetLatestTimestampAsync_ThenReturnsMaxTimestamp()
    {
        var candles = CreateCandles("BTC", "1h", 1000, 5);
        await using var context = CreateContext();
        var sut = new CandleRepository(context);
        await sut.BulkInsertAsync(candles);

        await using var queryContext = CreateContext();
        var querySut = new CandleRepository(queryContext);
        var result = await querySut.GetLatestTimestampAsync("BTC", "1h");

        result.Should().Be(5000);
    }

    [TestMethod]
    public async Task GivenSourceFilter_WhenGetLatestTimestampAsync_ThenReturnsMaxTimestampForMatchingSource()
    {
        var candles = new[]
        {
            Candle.Create("BTC", "1h", 1000, 100m, 105m, 95m, 102m, 50m, 10, source: "Hyperliquid"),
            Candle.Create("BTC", "1h", 2000, 100m, 105m, 95m, 102m, 50m, 10, source: "Binance"),
            Candle.Create("BTC", "1h", 3000, 100m, 105m, 95m, 102m, 50m, 10, source: "Binance")
        };

        await using var context = CreateContext();
        var sut = new CandleRepository(context);
        await sut.BulkInsertAsync(candles);

        await using var queryContext = CreateContext();
        var querySut = new CandleRepository(queryContext);
        var result = await querySut.GetLatestTimestampAsync("BTC", "1h", source: "Binance");

        result.Should().Be(3000);
    }

    [TestMethod]
    public async Task GivenNoCandlesExist_WhenGetLatestTimestampAsync_ThenReturnsNull()
    {
        await using var context = CreateContext();
        var sut = new CandleRepository(context);

        var result = await sut.GetLatestTimestampAsync("BTC", "15m");

        result.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenLargeBatch_WhenBulkInsertAsync_ThenProcessesInBatches()
    {
        var candles = CreateCandles("BTC", "15m", 1000, 1200);
        await using var context = CreateContext();
        var sut = new CandleRepository(context);

        await sut.BulkInsertAsync(candles);

        await using var verifyContext = CreateContext();
        var stored = await verifyContext.Candles.ToListAsync();
        stored.Should().HaveCount(1200);
    }

    [TestMethod]
    public async Task GivenCandlesWithDecimalPrices_WhenBulkInsertAndQuery_ThenPrecisionIsWithinAcceptableTolerance()
    {
        var candle = Candle.Create("BTC", "15m", 1000, 67234.56m, 67500.12m, 67100.99m, 67300.45m, 1234.5678m, 42);
        await using var context = CreateContext();
        var sut = new CandleRepository(context);
        await sut.BulkInsertAsync(new[] { candle });

        await using var queryContext = CreateContext();
        var querySut = new CandleRepository(queryContext);
        var result = await querySut.GetCandlesAsync("BTC", "15m", 1000, 1000);

        var stored = result.Single();
        stored.Source.Should().Be("Hyperliquid");
        stored.Open.Should().BeApproximately(67234.56m, 0.01m);
        stored.High.Should().BeApproximately(67500.12m, 0.01m);
        stored.Low.Should().BeApproximately(67100.99m, 0.01m);
        stored.Close.Should().BeApproximately(67300.45m, 0.01m);
        stored.Volume.Should().BeApproximately(1234.5678m, 0.001m);
    }

    private static List<Candle> CreateCandles(string symbol, string interval, long startTimestamp, int count, string source = "Hyperliquid")
    {
        return Enumerable.Range(0, count)
            .Select(i => Candle.Create(
                symbol,
                interval,
                startTimestamp + (i * 1000),
                100m + i,
                105m + i,
                95m + i,
                102m + i,
                50m + i,
                10 + i,
                source))
            .ToList();
    }
}
