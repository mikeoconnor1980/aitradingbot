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
            marketMetadataProvider: null);

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
            marketMetadataProvider: null);

        var candle = CreateCandle(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        sut.UpdateIndicators(candle);

        var context = await sut.BuildAsync(candle, candle, candle, requiredIndicators: null);

        context.FearGreed.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenExistingIndicatorHistory_WhenReset_ThenNextSessionStartsFromCleanState()
    {
        var now = DateTimeOffset.UtcNow;
        var sut = new LiveMarketContextBuilder(
            llmContextProvider: null,
            fearGreedSnapshotProvider: null,
            serviceScopeFactory: null,
            marketMetadataProvider: null);

        var firstCandle = CreateCandle(now.ToUnixTimeSeconds());
        sut.UpdateIndicators(firstCandle);

        sut.Reset();

        var secondCandle = CreateCandle(now.AddHours(1).ToUnixTimeSeconds());
        sut.UpdateIndicators(secondCandle);
        var context = await sut.BuildAsync(secondCandle, secondCandle, secondCandle, requiredIndicators: null);

        context.CandleHistory.Should().HaveCount(1);
        context.CandleHistory![0].Timestamp.Should().Be(secondCandle.Timestamp);
        context.PreviousCandle.Should().BeNull();
        context.Indicators!.EmaFast.Should().Be(0m);
        context.Indicators.EmaSlow.Should().Be(0m);
        context.Indicators.Atr.Should().Be(0m);
        context.Indicators.Rsi.Should().Be(50m);
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