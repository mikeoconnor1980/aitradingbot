using Microsoft.EntityFrameworkCore;
using TradePilot.Domain.Entities;
using TradePilot.Persistence.Repositories;

namespace TradePilot.Persistence.Tests.Repositories;

[TestClass]
public sealed class LlmContextSnapshotRepositoryTests
{
    private DbContextOptions<TradePilotDbContext> _contextOptions = null!;

    [TestInitialize]
    public void Setup()
    {
        _contextOptions = new DbContextOptionsBuilder<TradePilotDbContext>()
            .UseInMemoryDatabase(databaseName: $"LlmContextTests-{Guid.NewGuid():N}")
            .Options;

        using var context = new TradePilotDbContext(_contextOptions);
        context.Database.EnsureCreated();
    }

    private TradePilotDbContext CreateContext() => new(_contextOptions);

    [TestMethod]
    public async Task GivenSnapshot_WhenSaveAsync_ThenCanBeRetrieved()
    {
        var snapshot = LlmContextSnapshot.Create(
            "BTC-USD", "Bullish", "Neutral", "Low", 0.85m, "Normal", "All clear.", 1712000000000);

        await using (var writeCtx = CreateContext())
        {
            var sut = new LlmContextSnapshotRepository(writeCtx);
            await sut.SaveAsync(snapshot);
        }

        await using (var readCtx = CreateContext())
        {
            var sut = new LlmContextSnapshotRepository(readCtx);
            var result = await sut.GetLatestAsync("BTC-USD");

            result.Should().NotBeNull();
            result!.Symbol.Should().Be("BTC-USD");
            result.MarketSentiment.Should().Be("Bullish");
            result.MacroRegime.Should().Be("Neutral");
            result.EventRisk.Should().Be("Low");
            result.Confidence.Should().Be(0.85m);
            result.DerivedRegime.Should().Be("Normal");
            result.Summary.Should().Be("All clear.");
        }
    }

    [TestMethod]
    public async Task GivenMultipleSnapshots_WhenGetLatestAsync_ThenReturnsMostRecent()
    {
        var older = LlmContextSnapshot.Create(
            "BTC-USD", "Bearish", "Bearish", "High", 0.70m, "RiskOff", "Bearish.", 1712000000000);
        var newer = LlmContextSnapshot.Create(
            "BTC-USD", "Bullish", "Neutral", "Low", 0.90m, "Aggressive", "Bullish.", 1712003600000);

        await using (var writeCtx = CreateContext())
        {
            var sut = new LlmContextSnapshotRepository(writeCtx);
            await sut.SaveAsync(older);
            await sut.SaveAsync(newer);
        }

        await using (var readCtx = CreateContext())
        {
            var sut = new LlmContextSnapshotRepository(readCtx);
            var result = await sut.GetLatestAsync("BTC-USD");

            result.Should().NotBeNull();
            result!.DerivedRegime.Should().Be("Aggressive");
            result.GeneratedAtUtc.Should().Be(1712003600000);
        }
    }

    [TestMethod]
    public async Task GivenDifferentSymbols_WhenGetLatestAsync_ThenFiltersCorrectly()
    {
        var btc = LlmContextSnapshot.Create(
            "BTC-USD", "Bullish", "Neutral", "Low", 0.85m, "Normal", "BTC ok.", 1712000000000);
        var eth = LlmContextSnapshot.Create(
            "ETH-USD", "Bearish", "Bearish", "High", 0.60m, "Defensive", "ETH weak.", 1712000000000);

        await using (var writeCtx = CreateContext())
        {
            var sut = new LlmContextSnapshotRepository(writeCtx);
            await sut.SaveAsync(btc);
            await sut.SaveAsync(eth);
        }

        await using (var readCtx = CreateContext())
        {
            var sut = new LlmContextSnapshotRepository(readCtx);
            var result = await sut.GetLatestAsync("ETH-USD");

            result.Should().NotBeNull();
            result!.Symbol.Should().Be("ETH-USD");
            result.DerivedRegime.Should().Be("Defensive");
        }
    }

    [TestMethod]
    public async Task GivenNoSnapshots_WhenGetLatestAsync_ThenReturnsNull()
    {
        await using var ctx = CreateContext();
        var sut = new LlmContextSnapshotRepository(ctx);

        var result = await sut.GetLatestAsync("BTC-USD");

        result.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenSnapshots_WhenGetHistoryAsync_ThenReturnsInTimeRange()
    {
        var s1 = LlmContextSnapshot.Create(
            "BTC-USD", "Neutral", "Neutral", "Low", 0.70m, "Normal", "S1", 1712000000000);
        var s2 = LlmContextSnapshot.Create(
            "BTC-USD", "Bullish", "Neutral", "Low", 0.80m, "Aggressive", "S2", 1712003600000);
        var s3 = LlmContextSnapshot.Create(
            "BTC-USD", "Bearish", "Bearish", "High", 0.90m, "RiskOff", "S3", 1712007200000);

        await using (var writeCtx = CreateContext())
        {
            var sut = new LlmContextSnapshotRepository(writeCtx);
            await sut.SaveAsync(s1);
            await sut.SaveAsync(s2);
            await sut.SaveAsync(s3);
        }

        await using (var readCtx = CreateContext())
        {
            var sut = new LlmContextSnapshotRepository(readCtx);
            var result = await sut.GetHistoryAsync("BTC-USD", 1712000000000, 1712003600000);

            result.Should().HaveCount(2);
            result[0].Summary.Should().Be("S1");
            result[1].Summary.Should().Be("S2");
        }
    }
}
