using TradePilot.Application.Scheduling.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Scheduling;

/// <summary>
/// Detects candle close transitions and emits CandleClosedEvent exactly once per candle.
/// Shared between live trading and backtesting.
/// </summary>
public sealed class CandleClock
{
    private readonly Dictionary<string, long> _lastClosed = new();

    public event Func<CandleClosedEvent, Task>? CandleClosed;

    public void Reset()
    {
        _lastClosed.Clear();
    }

    public async Task ProcessCandleAsync(Candle candle)
    {
        ArgumentNullException.ThrowIfNull(candle);

        var key = $"{candle.Symbol}:{candle.Interval}";
        var closeTimeUtc = candle.Timestamp + GetIntervalMs(candle.Interval);

        if (_lastClosed.TryGetValue(key, out var lastCloseTime) &&
            lastCloseTime >= closeTimeUtc)
        {
            return;
        }

        _lastClosed[key] = closeTimeUtc;

        if (CandleClosed is not null)
        {
            var closedEvent = new CandleClosedEvent
            {
                Symbol = candle.Symbol,
                Timeframe = candle.Interval,
                OpenTimeUtc = candle.Timestamp,
                CloseTimeUtc = closeTimeUtc,
                Candle = candle
            };

            foreach (var handler in CandleClosed.GetInvocationList().Cast<Func<CandleClosedEvent, Task>>())
            {
                await handler(closedEvent);
            }
        }
    }

    private static long GetIntervalMs(string interval) => interval switch
    {
        "5m" => 5L * 60L * 1000L,
        "15m" => 15L * 60L * 1000L,
        "1h" => 60L * 60L * 1000L,
        "4h" => 4L * 60L * 60L * 1000L,
        _ => throw new ArgumentException($"Unsupported interval: {interval}", nameof(interval))
    };
}
