using System.Collections.Concurrent;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Scheduling;

/// <summary>
/// Thread-safe shared state store that tracks in-progress (partial) candle data
/// per symbol/timeframe. The WebSocket trade feed writes here; <see cref="CandleBuilder"/>
/// reads from here to detect candle close boundaries.
/// </summary>
public sealed class MarketStateStore
{
    private readonly ConcurrentDictionary<string, CandleAccumulator> _accumulators = new();

    public CandleAccumulator GetOrCreate(string symbol, string interval, long bucketTimestamp)
    {
        var key = BuildKey(symbol, interval);

        return _accumulators.AddOrUpdate(
            key,
            _ => CandleAccumulator.Create(symbol, interval, bucketTimestamp),
            (_, existing) =>
            {
                if (existing.BucketTimestamp == bucketTimestamp)
                {
                    return existing;
                }

                // New bucket — replace accumulator
                return CandleAccumulator.Create(symbol, interval, bucketTimestamp);
            });
    }

    public CandleAccumulator? TryGet(string symbol, string interval)
    {
        return _accumulators.TryGetValue(BuildKey(symbol, interval), out var acc) ? acc : null;
    }

    public bool TryRemove(string symbol, string interval)
    {
        return _accumulators.TryRemove(BuildKey(symbol, interval), out _);
    }

    private static string BuildKey(string symbol, string interval) => $"{symbol}:{interval}";
}

/// <summary>
/// Accumulates trade ticks into OHLCV data for a single candle bucket.
/// Thread-safe via lock on mutation methods.
/// </summary>
public sealed class CandleAccumulator
{
    private readonly object _lock = new();

    public string Symbol { get; }
    public string Interval { get; }
    public long BucketTimestamp { get; }

    public decimal Open { get; private set; }
    public decimal High { get; private set; }
    public decimal Low { get; private set; }
    public decimal Close { get; private set; }
    public decimal Volume { get; private set; }
    public int NumTrades { get; private set; }
    public bool HasData { get; private set; }

    private CandleAccumulator(string symbol, string interval, long bucketTimestamp)
    {
        Symbol = symbol;
        Interval = interval;
        BucketTimestamp = bucketTimestamp;
    }

    public static CandleAccumulator Create(string symbol, string interval, long bucketTimestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bucketTimestamp);

        return new CandleAccumulator(symbol, interval, bucketTimestamp);
    }

    public void AddTick(decimal price, decimal size)
    {
        lock (_lock)
        {
            if (!HasData)
            {
                Open = price;
                High = price;
                Low = price;
                Close = price;
                Volume = size;
                NumTrades = 1;
                HasData = true;
                return;
            }

            if (price > High) High = price;
            if (price < Low) Low = price;
            Close = price;
            Volume += size;
            NumTrades++;
        }
    }

    public Candle ToCandle(string source = "Hyperliquid")
    {
        lock (_lock)
        {
            if (!HasData)
            {
                throw new InvalidOperationException("Cannot create candle from empty accumulator.");
            }

            return Candle.Create(
                source,
                Symbol,
                Interval,
                BucketTimestamp,
                Open,
                High,
                Low,
                Close,
                Volume,
                NumTrades);
        }
    }
}
