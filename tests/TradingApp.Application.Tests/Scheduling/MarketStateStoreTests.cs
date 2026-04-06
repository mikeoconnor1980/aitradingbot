using TradingApp.Application.Scheduling;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Scheduling;

[TestClass]
public sealed class MarketStateStoreTests
{
    private MarketStateStore _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new MarketStateStore();
    }

    [TestMethod]
    public void GivenEmptyStore_WhenGetOrCreate_ThenCreatesNewAccumulator()
    {
        var acc = _sut.GetOrCreate("BTC-PERP", "15m", 1000L);

        acc.Should().NotBeNull();
        acc.Symbol.Should().Be("BTC-PERP");
        acc.Interval.Should().Be("15m");
        acc.BucketTimestamp.Should().Be(1000L);
        acc.HasData.Should().BeFalse();
    }

    [TestMethod]
    public void GivenExistingAccumulator_WhenGetOrCreateSameBucket_ThenReturnsSameAccumulator()
    {
        var first = _sut.GetOrCreate("BTC-PERP", "15m", 1000L);
        first.AddTick(50000m, 0.1m);

        var second = _sut.GetOrCreate("BTC-PERP", "15m", 1000L);

        second.HasData.Should().BeTrue();
        second.Open.Should().Be(50000m);
    }

    [TestMethod]
    public void GivenExistingAccumulator_WhenGetOrCreateNewBucket_ThenReplacesAccumulator()
    {
        var first = _sut.GetOrCreate("BTC-PERP", "15m", 1000L);
        first.AddTick(50000m, 0.1m);

        var second = _sut.GetOrCreate("BTC-PERP", "15m", 2000L);

        second.HasData.Should().BeFalse();
        second.BucketTimestamp.Should().Be(2000L);
    }

    [TestMethod]
    public void GivenExistingAccumulator_WhenTryGet_ThenReturnsAccumulator()
    {
        _sut.GetOrCreate("BTC-PERP", "15m", 1000L);

        var result = _sut.TryGet("BTC-PERP", "15m");

        result.Should().NotBeNull();
    }

    [TestMethod]
    public void GivenEmptyStore_WhenTryGet_ThenReturnsNull()
    {
        var result = _sut.TryGet("BTC-PERP", "15m");

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenExistingAccumulator_WhenTryRemove_ThenRemovesAndReturnsTrue()
    {
        _sut.GetOrCreate("BTC-PERP", "15m", 1000L);

        var removed = _sut.TryRemove("BTC-PERP", "15m");

        removed.Should().BeTrue();
        _sut.TryGet("BTC-PERP", "15m").Should().BeNull();
    }

    [TestMethod]
    public void GivenDifferentSymbols_WhenGetOrCreate_ThenTracksIndependently()
    {
        var btc = _sut.GetOrCreate("BTC-PERP", "15m", 1000L);
        btc.AddTick(50000m, 0.1m);

        var eth = _sut.GetOrCreate("ETH-PERP", "15m", 1000L);

        eth.HasData.Should().BeFalse();
        btc.HasData.Should().BeTrue();
    }
}

[TestClass]
public sealed class CandleAccumulatorTests
{
    [TestMethod]
    public void GivenNewAccumulator_WhenAddFirstTick_ThenSetsOhlcvCorrectly()
    {
        var acc = CandleAccumulator.Create("BTC-PERP", "15m", 1000L);

        acc.AddTick(50000m, 0.5m);

        acc.HasData.Should().BeTrue();
        acc.Open.Should().Be(50000m);
        acc.High.Should().Be(50000m);
        acc.Low.Should().Be(50000m);
        acc.Close.Should().Be(50000m);
        acc.Volume.Should().Be(0.5m);
        acc.NumTrades.Should().Be(1);
    }

    [TestMethod]
    public void GivenAccumulatorWithData_WhenAddMoreTicks_ThenUpdatesOhlcvCorrectly()
    {
        var acc = CandleAccumulator.Create("BTC-PERP", "15m", 1000L);

        acc.AddTick(50000m, 0.5m);  // Open
        acc.AddTick(50500m, 0.3m);  // New High
        acc.AddTick(49800m, 0.2m);  // New Low
        acc.AddTick(50100m, 0.1m);  // Close

        acc.Open.Should().Be(50000m);
        acc.High.Should().Be(50500m);
        acc.Low.Should().Be(49800m);
        acc.Close.Should().Be(50100m);
        acc.Volume.Should().Be(1.1m);
        acc.NumTrades.Should().Be(4);
    }

    [TestMethod]
    public void GivenAccumulatorWithData_WhenToCandle_ThenReturnsCorrectCandle()
    {
        var acc = CandleAccumulator.Create("BTC-PERP", "15m", 900_000L);
        acc.AddTick(50000m, 0.5m);
        acc.AddTick(50500m, 0.3m);

        var candle = acc.ToCandle();

        candle.Symbol.Should().Be("BTC-PERP");
        candle.Interval.Should().Be("15m");
        candle.Timestamp.Should().Be(900_000L);
        candle.Open.Should().Be(50000m);
        candle.High.Should().Be(50500m);
        candle.Low.Should().Be(50000m);
        candle.Close.Should().Be(50500m);
        candle.Volume.Should().Be(0.8m);
        candle.NumTrades.Should().Be(2);
    }

    [TestMethod]
    public void GivenEmptyAccumulator_WhenToCandle_ThenThrowsInvalidOperationException()
    {
        var acc = CandleAccumulator.Create("BTC-PERP", "15m", 1000L);

        var act = () => acc.ToCandle();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*empty accumulator*");
    }
}
