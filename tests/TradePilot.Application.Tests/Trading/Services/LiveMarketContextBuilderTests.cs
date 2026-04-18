using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.Trading.Services;

[TestClass]
public sealed class LiveMarketContextBuilderTests
{
    [TestMethod]
    public async Task GivenControlPlaneFearGreedSnapshot_WhenBuildAsync_ThenIncludesFearGreedInMarketContext()
    {
        var now = DateTimeOffset.UtcNow;
        var fearGreedProvider = new Mock<IFearGreedSnapshotProvider>();
        fearGreedProvider
            .Setup(provider => provider.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FearGreedSnapshot(26, FearGreedClassification.Fear, now.ToUnixTimeSeconds()));

        var sut = new LiveMarketContextBuilder(
            llmContextProvider: null,
            fearGreedSnapshotProvider: fearGreedProvider.Object,
            serviceScopeFactory: null,
            restClient: null);

        var candle = CreateCandle(now.ToUnixTimeSeconds());
        sut.UpdateIndicators(candle);

        var context = await sut.BuildAsync(candle, candle, candle, requiredIndicators: null);

        context.FearGreed.Should().NotBeNull();
        context.FearGreed!.Value.Should().Be(26);
        context.FearGreed.Classification.Should().Be(FearGreedClassification.Fear);
    }

    [TestMethod]
    public async Task GivenStaleControlPlaneFearGreedSnapshot_WhenBuildAsync_ThenOmitsFearGreedFromMarketContext()
    {
        var staleTimestamp = DateTimeOffset.UtcNow.AddHours(-49).ToUnixTimeSeconds();
        var fearGreedProvider = new Mock<IFearGreedSnapshotProvider>();
        fearGreedProvider
            .Setup(provider => provider.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FearGreedSnapshot(26, FearGreedClassification.Fear, staleTimestamp));

        var sut = new LiveMarketContextBuilder(
            llmContextProvider: null,
            fearGreedSnapshotProvider: fearGreedProvider.Object,
            serviceScopeFactory: null,
            restClient: null);

        var candle = CreateCandle(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        sut.UpdateIndicators(candle);

        var context = await sut.BuildAsync(candle, candle, candle, requiredIndicators: null);

        context.FearGreed.Should().BeNull();
    }

    private static Candle CreateCandle(long timestamp)
    {
        return Candle.Create(
            symbol: "BTC-PERP",
            interval: "1h",
            timestamp: timestamp,
            open: 78000m,
            high: 78500m,
            low: 77500m,
            close: 78250m,
            volume: 1m,
            numTrades: 1);
    }
}