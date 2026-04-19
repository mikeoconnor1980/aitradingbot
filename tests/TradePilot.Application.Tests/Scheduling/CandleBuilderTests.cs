using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.Scheduling;
using TradePilot.Application.Scheduling.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.Scheduling;

[TestClass]
public sealed class CandleBuilderTests
{
    private MarketStateStore _stateStore = default!;
    private CandleClock _candleClock = default!;
    private Mock<ICandleRepository> _candleRepositoryMock = default!;
    private Mock<IServiceScopeFactory> _scopeFactoryMock = default!;
    private CandleBuilder _sut = default!;
    private List<CandleClosedEvent> _emittedEvents = default!;

    // Use a realistic epoch timestamp base (2024-01-01 00:00:00 UTC in ms)
    // This ensures all interval buckets (15m, 1h, 4h) produce non-zero timestamps
    private const long EpochBase = 1_704_067_200_000L;

    // 15-minute interval in milliseconds
    private const long FifteenMinutesMs = 15L * 60L * 1000L;

    [TestInitialize]
    public void Setup()
    {
        _stateStore = new MarketStateStore();
        _candleClock = new CandleClock();
        _candleRepositoryMock = new Mock<ICandleRepository>();
        _emittedEvents = [];

        var scopeMock = new Mock<IServiceScope>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ICandleRepository)))
            .Returns(_candleRepositoryMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        _candleClock.CandleClosed += evt =>
        {
            _emittedEvents.Add(evt);
            return Task.CompletedTask;
        };

        _sut = new CandleBuilder(
            _stateStore,
            _candleClock,
            _scopeFactoryMock.Object,
            Mock.Of<ILogger<CandleBuilder>>());
    }

    [TestMethod]
    public async Task GivenFirstTrade_WhenProcessTick_ThenNoConfirmedCandleEmitted()
    {
        // A single trade within the first 15m bucket — no candle should close yet
        var tick = CreateTick("BTC-PERP", 50000m, 0.1m, EpochBase + 1000);

        await _sut.ProcessTickAsync(tick);

        _emittedEvents.Should().BeEmpty();
        _candleRepositoryMock.Verify(
            r => r.BulkInsertAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenTradesInSameBucket_WhenProcessTick_ThenAccumulatesWithoutEmitting()
    {
        var bucketStart = EpochBase;

        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50000m, 0.1m, bucketStart + 1000));
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50500m, 0.2m, bucketStart + 5000));
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 49800m, 0.15m, bucketStart + 10000));

        _emittedEvents.Should().BeEmpty();

        // Verify the accumulator has the correct state
        var acc = _stateStore.TryGet("BTC-PERP", "15m");
        acc.Should().NotBeNull();
        acc!.Open.Should().Be(50000m);
        acc.High.Should().Be(50500m);
        acc.Low.Should().Be(49800m);
        acc.Close.Should().Be(49800m);
        acc.Volume.Should().Be(0.45m);
        acc.NumTrades.Should().Be(3);
    }

    [TestMethod]
    public async Task GivenBoundaryTrade_WhenNextBucketTradeComes_ThenEmitsConfirmedCandle()
    {
        // First bucket: aligned to a 15m boundary
        var bucket1Start = EpochBase;
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50000m, 0.1m, bucket1Start + 1000));
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50500m, 0.2m, bucket1Start + 5000));

        // Second bucket: first trade crosses into the next 15m period
        var bucket2Start = EpochBase + FifteenMinutesMs;
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50100m, 0.3m, bucket2Start + 1));

        // The first bucket's candle should now be confirmed and emitted
        var event15m = _emittedEvents.Single(evt => evt.Timeframe == "15m");
        event15m.Symbol.Should().Be("BTC-PERP");
        event15m.Candle.Open.Should().Be(50000m);
        event15m.Candle.High.Should().Be(50500m);
        event15m.Candle.Close.Should().Be(50500m);
    }

    [TestMethod]
    public async Task GivenConfirmedCandle_WhenEmitted_ThenPersistedViaCandleRepository()
    {
        var bucket1Start = EpochBase;
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50000m, 0.1m, bucket1Start + 1000));

        var bucket2Start = EpochBase + FifteenMinutesMs;
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50100m, 0.3m, bucket2Start + 1));

        _candleRepositoryMock.Verify(
            r => r.BulkInsertAsync(
                It.Is<IEnumerable<Candle>>(candles => candles.Any(c =>
                    c.Symbol == "BTC-PERP" &&
                    c.Interval == "15m" &&
                    c.Open == 50000m)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenMultipleTimeframes_WhenBoundaryCrossed_ThenEmitsForAllCrossedTimeframes()
    {
        // EpochBase is 2024-01-01 00:00:00 UTC — aligned to 15m, 1h, and 4h boundaries
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50000m, 0.1m, EpochBase + 1000));

        // Move to the next 15m bucket (but still same 1h and 4h bucket)
        var next15m = EpochBase + FifteenMinutesMs;
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50100m, 0.2m, next15m + 1));

        // Should emit a 15m candle closed event via CandleClock
        // The 1h and 4h candles should NOT be emitted yet
        var fifteenMinEvents = _emittedEvents.Where(e => e.Timeframe == "15m").ToList();
        var oneHourEvents = _emittedEvents.Where(e => e.Timeframe == "1h").ToList();

        fifteenMinEvents.Should().HaveCount(1);
        oneHourEvents.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenFiveMinuteBoundary_WhenNextBucketTradeComes_ThenEmitsConfirmedFiveMinuteCandle()
    {
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50000m, 0.1m, EpochBase + 1_000));

        var nextFiveMinuteBucket = EpochBase + (5L * 60L * 1000L);
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50100m, 0.2m, nextFiveMinuteBucket + 1));

        _emittedEvents.Should().ContainSingle(evt => evt.Timeframe == "5m");
    }

    [TestMethod]
    public async Task GivenStaleAccumulator_WhenResetBeforeNextSession_ThenNextTickDoesNotEmitOldCandle()
    {
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50000m, 0.1m, EpochBase + 1_000));

        _sut.Reset();

        var nextFiveMinuteBucket = EpochBase + (5L * 60L * 1000L);
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50100m, 0.2m, nextFiveMinuteBucket + 1));

        _emittedEvents.Should().BeEmpty();
        _candleRepositoryMock.Verify(
            r => r.BulkInsertAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenClosedFiveMinuteBucketWithoutFollowOnTrade_WhenFlushedByWallClock_ThenEmitsConfirmedFiveMinuteCandle()
    {
        var tradeTime = EpochBase + 1_000;
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50000m, 0.1m, tradeTime));

        var closeTime = EpochBase + (5L * 60L * 1000L);
        await _sut.FlushClosedCandlesAsync(DateTimeOffset.FromUnixTimeMilliseconds(closeTime));

        _emittedEvents.Should().ContainSingle(evt => evt.Timeframe == "5m");
        _candleRepositoryMock.Verify(
            r => r.BulkInsertAsync(
                It.Is<IEnumerable<Candle>>(candles => candles.Any(c =>
                    c.Symbol == "BTC-PERP" &&
                    c.Interval == "5m" &&
                    c.Timestamp == EpochBase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenOpenBucketBeforeCloseTime_WhenFlushedByWallClock_ThenDoesNotEmitCandle()
    {
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50000m, 0.1m, EpochBase + 1_000));

        var beforeClose = DateTimeOffset.FromUnixTimeMilliseconds(EpochBase + (5L * 60L * 1000L) - 1);
        await _sut.FlushClosedCandlesAsync(beforeClose);

        _emittedEvents.Should().BeEmpty();
        _candleRepositoryMock.Verify(
            r => r.BulkInsertAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public void GivenTimestamp_WhenGetBucketTimestamp_ThenAlignsToBucketBoundary()
    {
        // 15m = 900,000 ms
        // A trade at 12:17:30 (timestamp = 900,000 * 10 + 150,000 = 9,150,000)
        // should bucket to 12:15:00 (timestamp = 900,000 * 10 = 9,000,000)
        var tradeTimestamp = 9_150_000L;
        var intervalMs = FifteenMinutesMs;

        var bucket = CandleBuilder.GetBucketTimestamp(tradeTimestamp, intervalMs);

        bucket.Should().Be(9_000_000L);
    }

    [TestMethod]
    public void GivenTimestampExactlyOnBoundary_WhenGetBucketTimestamp_ThenReturnsExactTimestamp()
    {
        var tradeTimestamp = 9_000_000L;
        var intervalMs = FifteenMinutesMs;

        var bucket = CandleBuilder.GetBucketTimestamp(tradeTimestamp, intervalMs);

        bucket.Should().Be(9_000_000L);
    }

    [TestMethod]
    public async Task GivenGapInTrades_WhenNextTradeArrives_ThenOnlyEmitsForBucketWithData()
    {
        var bucket1Start = EpochBase;
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 50000m, 0.1m, bucket1Start + 1000));

        // Skip the next bucket entirely, trade arrives two buckets later
        var bucket3Start = EpochBase + (FifteenMinutesMs * 2);
        await _sut.ProcessTickAsync(CreateTick("BTC-PERP", 51000m, 0.2m, bucket3Start + 1));

        // Should emit bucket 10's candle (it had data), but nothing for bucket 11 (no trades)
        var fifteenMinEvents = _emittedEvents.Where(e => e.Timeframe == "15m").ToList();
        fifteenMinEvents.Should().HaveCount(1);
        fifteenMinEvents[0].Candle.Open.Should().Be(50000m);
    }

    private static TradeTickDto CreateTick(string asset, decimal price, decimal size, long timestampMs) => new()
    {
        Asset = asset,
        Price = price,
        Size = size,
        Side = "buy",
        TimestampMs = timestampMs
    };
}
