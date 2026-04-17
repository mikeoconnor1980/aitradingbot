using TradePilot.Application.Scheduling;
using TradePilot.Application.Scheduling.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.Scheduling;

[TestClass]
public sealed class CandleClockTests
{
    private CandleClock _sut = default!;
    private List<CandleClosedEvent> _emittedEvents = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new CandleClock();
        _emittedEvents = [];

        _sut.CandleClosed += evt =>
        {
            _emittedEvents.Add(evt);
            return Task.CompletedTask;
        };
    }

    [TestMethod]
    public async Task GivenNewCandle_WhenProcessCandleAsync_ThenEmitsCandleClosedEvent()
    {
        var candle = CreateCandle("BTC", "15m", timestampUtc: 1000);

        await _sut.ProcessCandleAsync(candle);

        _emittedEvents.Should().HaveCount(1);
        _emittedEvents[0].Symbol.Should().Be("BTC");
        _emittedEvents[0].Timeframe.Should().Be("15m");
        _emittedEvents[0].OpenTimeUtc.Should().Be(1000);
        _emittedEvents[0].CloseTimeUtc.Should().Be(1000 + (15L * 60L * 1000L));
    }

    [TestMethod]
    public async Task GivenDuplicateCandle_WhenProcessCandleAsync_ThenDoesNotEmitDuplicateEvent()
    {
        var candle = CreateCandle("BTC", "15m", timestampUtc: 1000);

        await _sut.ProcessCandleAsync(candle);
        await _sut.ProcessCandleAsync(candle);

        _emittedEvents.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenOlderCandle_WhenProcessCandleAsync_ThenIgnoresOlderCandle()
    {
        var newerCandle = CreateCandle("BTC", "15m", timestampUtc: 2000);
        var olderCandle = CreateCandle("BTC", "15m", timestampUtc: 1000);

        await _sut.ProcessCandleAsync(newerCandle);
        await _sut.ProcessCandleAsync(olderCandle);

        _emittedEvents.Should().HaveCount(1);
        _emittedEvents[0].OpenTimeUtc.Should().Be(2000);
    }

    [TestMethod]
    public async Task GivenDifferentTimeframes_WhenProcessCandleAsync_ThenTracksIndependently()
    {
        var candle15m = CreateCandle("BTC", "15m", timestampUtc: 1000);
        var candle1h = CreateCandle("BTC", "1h", timestampUtc: 1000);

        await _sut.ProcessCandleAsync(candle15m);
        await _sut.ProcessCandleAsync(candle1h);

        _emittedEvents.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task GivenUnsupportedInterval_WhenProcessCandleAsync_ThenThrowsArgumentException()
    {
        var candle = CreateCandle("BTC", "2m", timestampUtc: 1000);

        var act = () => _sut.ProcessCandleAsync(candle);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unsupported interval*");
    }

    private static Candle CreateCandle(string symbol, string interval, long timestampUtc)
    {
        return Candle.Create(
            symbol,
            interval,
            timestampUtc,
            100m,
            105m,
            95m,
            102m,
            1000m,
            10);
    }
}
