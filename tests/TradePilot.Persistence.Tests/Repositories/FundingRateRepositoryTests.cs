using Microsoft.EntityFrameworkCore;
using TradePilot.Domain.Entities;
using TradePilot.Persistence.Repositories;

namespace TradePilot.Persistence.Tests.Repositories;

[TestClass]
public sealed class FundingRateRepositoryTests
{
    private DbContextOptions<TradePilotDbContext> _contextOptions = null!;

    [TestInitialize]
    public void Setup()
    {
        _contextOptions = new DbContextOptionsBuilder<TradePilotDbContext>()
            .UseInMemoryDatabase(databaseName: $"FundingRateTests-{Guid.NewGuid():N}")
            .Options;

        using var context = new TradePilotDbContext(_contextOptions);
        context.Database.EnsureCreated();
    }

    [TestMethod]
    public async Task GivenFundingRates_WhenBulkInsertAsync_ThenAllFundingRatesArePersisted()
    {
        var fundingRates = CreateFundingRates("BTC", 1000, 3);

        await using var context = CreateContext();
        var sut = new FundingRateRepository(context);

        await sut.BulkInsertAsync(fundingRates);

        await using var verifyContext = CreateContext();
        var stored = await verifyContext.FundingRates
            .OrderBy(rate => rate.Timestamp)
            .ToListAsync();

        stored.Should().HaveCount(3);
        stored.Select(rate => rate.Timestamp).Should().Equal(1000, 2000, 3000);
    }

    [TestMethod]
    public async Task GivenDuplicateFundingRates_WhenBulkInsertAsync_ThenDuplicatesAreSkipped()
    {
        var fundingRates = CreateFundingRates("BTC", 1000, 3);

        await using var context = CreateContext();
        var sut = new FundingRateRepository(context);
        await sut.BulkInsertAsync(fundingRates);

        var duplicatesWithNew = fundingRates.Concat([FundingRate.Create("BTC", 4000, 0.0004m, 53000m)]);

        await using var secondContext = CreateContext();
        var secondSut = new FundingRateRepository(secondContext);
        await secondSut.BulkInsertAsync(duplicatesWithNew);

        await using var verifyContext = CreateContext();
        var stored = await verifyContext.FundingRates.ToListAsync();

        stored.Should().HaveCount(4);
    }

    [TestMethod]
    public async Task GivenFundingRatesExist_WhenGetLatestTimestampAsync_ThenReturnsLatestTimestamp()
    {
        var fundingRates = CreateFundingRates("BTC", 1000, 5);

        await using var context = CreateContext();
        var sut = new FundingRateRepository(context);
        await sut.BulkInsertAsync(fundingRates);

        await using var queryContext = CreateContext();
        var querySut = new FundingRateRepository(queryContext);
        var result = await querySut.GetLatestTimestampAsync("BTC");

        result.Should().Be(5000);
    }

    [TestMethod]
    public async Task GivenFundingRatesDoNotExist_WhenGetLatestTimestampAsync_ThenReturnsNull()
    {
        await using var context = CreateContext();
        var sut = new FundingRateRepository(context);

        var result = await sut.GetLatestTimestampAsync("BTC");

        result.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenFundingRatesExist_WhenGetRangeAsync_ThenReturnsOrderedRangeWithinBounds()
    {
        var fundingRates = CreateFundingRates("BTC", 1000, 5);
        var otherSymbolRate = FundingRate.Create("ETH", 2500, 0.0009m, 60000m);

        await using var context = CreateContext();
        var sut = new FundingRateRepository(context);
        await sut.BulkInsertAsync(fundingRates.Concat([otherSymbolRate]));

        await using var queryContext = CreateContext();
        var querySut = new FundingRateRepository(queryContext);
        var result = await querySut.GetRangeAsync("BTC", 1500, 4500);

        result.Select(rate => rate.Timestamp).Should().Equal(2000, 3000, 4000);
        result.Should().OnlyContain(rate => rate.Symbol == "BTC");
    }

    [TestMethod]
    public async Task GivenFundingRatesWithDecimals_WhenBulkInsertAsync_ThenPersistsWithinTolerance()
    {
        var fundingRate = FundingRate.Create("BTC", 1000, 0.00012345m, 51234.5678m);

        await using var context = CreateContext();
        var sut = new FundingRateRepository(context);
        await sut.BulkInsertAsync([fundingRate]);

        await using var verifyContext = CreateContext();
        var stored = await verifyContext.FundingRates.SingleAsync();

        stored.Rate.Should().BeApproximately(0.00012345m, 0.00000001m);
        stored.MarkPrice.Should().BeApproximately(51234.5678m, 0.0001m);
    }

    private TradePilotDbContext CreateContext() => new(_contextOptions);

    private static List<FundingRate> CreateFundingRates(string symbol, long startTimestamp, int count)
    {
        return Enumerable.Range(0, count)
            .Select(index => FundingRate.Create(
                symbol,
                startTimestamp + (index * 1000),
                0.0001m + (index * 0.0001m),
                50000m + index))
            .ToList();
    }
}