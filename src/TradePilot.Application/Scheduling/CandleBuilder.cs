using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Scheduling;

/// <summary>
/// Assembles confirmed OHLCV candles from the WebSocket trade tick stream.
/// Detects candle close boundaries by timestamp (not wall-clock time) and feeds
/// confirmed candles to <see cref="CandleClock.ProcessCandleAsync"/> for downstream
/// strategy scheduling.
/// </summary>
public sealed class CandleBuilder
{
    private static readonly string[] SupportedIntervals = ["15m", "1h", "4h"];

    private readonly MarketStateStore _stateStore;
    private readonly CandleClock _candleClock;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CandleBuilder> _logger;

    public CandleBuilder(
        MarketStateStore stateStore,
        CandleClock candleClock,
        IServiceScopeFactory scopeFactory,
        ILogger<CandleBuilder> logger)
    {
        _stateStore = stateStore;
        _candleClock = candleClock;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ProcessTickAsync(TradeTickDto tick)
    {
        ArgumentNullException.ThrowIfNull(tick);

        foreach (var interval in SupportedIntervals)
        {
            var intervalMs = GetIntervalMs(interval);
            var bucketTimestamp = GetBucketTimestamp(tick.TimestampMs, intervalMs);

            // Check if there's an existing accumulator from a previous bucket
            var existing = _stateStore.TryGet(tick.Asset, interval);

            if (existing is not null &&
                existing.BucketTimestamp < bucketTimestamp &&
                existing.HasData)
            {
                // The existing candle has closed — emit it before switching to the new bucket
                await EmitConfirmedCandleAsync(existing);
            }

            // Get or create the accumulator for the current bucket and add the tick
            var accumulator = _stateStore.GetOrCreate(tick.Asset, interval, bucketTimestamp);
            accumulator.AddTick(tick.Price, tick.Size);
        }
    }

    private async Task EmitConfirmedCandleAsync(CandleAccumulator accumulator)
    {
        Candle candle;
        try
        {
            candle = accumulator.ToCandle();
        }
        catch (InvalidOperationException)
        {
            // Empty accumulator — no trades in this bucket, skip
            return;
        }

        _logger.LogInformation(
            "Candle confirmed: {Symbol} {Interval} @ {Timestamp}, O={Open} H={High} L={Low} C={Close} V={Volume}",
            candle.Symbol, candle.Interval, candle.Timestamp,
            candle.Open, candle.High, candle.Low, candle.Close, candle.Volume);

        // Persist the confirmed candle
        using (var scope = _scopeFactory.CreateScope())
        {
            var candleRepository = scope.ServiceProvider.GetRequiredService<ICandleRepository>();
            await candleRepository.BulkInsertAsync([candle]);
        }

        // Feed to CandleClock for downstream strategy scheduling
        await _candleClock.ProcessCandleAsync(candle);

        // Clean up the old accumulator (it's already been replaced in the store by GetOrCreate)
        _stateStore.TryRemove(accumulator.Symbol, accumulator.Interval);
    }

    /// <summary>
    /// Computes the bucket start timestamp for a given trade timestamp and interval.
    /// E.g., for a 15m interval: a trade at 12:17:30 belongs to the 12:15:00 bucket.
    /// </summary>
    public static long GetBucketTimestamp(long tradeTimestampMs, long intervalMs)
    {
        return tradeTimestampMs / intervalMs * intervalMs;
    }

    private static long GetIntervalMs(string interval) => interval switch
    {
        "15m" => 15L * 60L * 1000L,
        "1h" => 60L * 60L * 1000L,
        "4h" => 4L * 60L * 60L * 1000L,
        _ => throw new ArgumentException($"Unsupported interval: {interval}", nameof(interval))
    };
}
