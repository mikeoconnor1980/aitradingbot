using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Backtesting.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Backtesting.Services;

/// <summary>
/// Reads historical candles from the database and prepares replay data with
/// aligned higher-timeframe context for deterministic backtests.
/// </summary>
public sealed class CandleReplayEngine
{
    private const string OneHourInterval = "1h";
    private const string FourHourInterval = "4h";

    private static readonly HashSet<string> SupportedTriggerIntervals = new(StringComparer.OrdinalIgnoreCase)
    {
        "15m", "1h", "4h"
    };

    private readonly ICandleRepository _candleRepository;

    public CandleReplayEngine(ICandleRepository candleRepository)
    {
        _candleRepository = candleRepository ?? throw new ArgumentNullException(nameof(candleRepository));
    }

    public async Task<ReplayData> LoadAsync(BacktestConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var triggerTimeframe = config.TriggerTimeframe;
        if (!SupportedTriggerIntervals.Contains(triggerTimeframe))
        {
            throw new ArgumentException($"Unsupported trigger timeframe: {triggerTimeframe}. Supported: 15m, 1h, 4h");
        }

        var warmupStartUtc = CalculateWarmupStartUtc(config, triggerTimeframe);
        var candles15mTask = _candleRepository.GetCandlesAsync(
            config.Symbol,
            "15m",
            warmupStartUtc,
            config.EndDateUtc,
            cancellationToken: cancellationToken);

        var candles1hTask = _candleRepository.GetCandlesAsync(
            config.Symbol,
            OneHourInterval,
            CalculateHigherTimeframeQueryStartUtc(warmupStartUtc, OneHourInterval),
            config.EndDateUtc,
            cancellationToken: cancellationToken);

        var candles4hTask = _candleRepository.GetCandlesAsync(
            config.Symbol,
            FourHourInterval,
            CalculateHigherTimeframeQueryStartUtc(warmupStartUtc, FourHourInterval),
            config.EndDateUtc,
            cancellationToken: cancellationToken);

        await Task.WhenAll(candles15mTask, candles1hTask, candles4hTask);

        var sorted15m = DeduplicateByTimestamp(candles15mTask.Result);
        var sorted1h = DeduplicateByTimestamp(candles1hTask.Result);
        var sorted4h = DeduplicateByTimestamp(candles4hTask.Result);

        var triggerCandles = GetCandlesByTimeframe(triggerTimeframe, sorted15m, sorted1h, sorted4h);
        var warmupEndIndex = DetermineWarmupEndIndex(triggerCandles, config);

        ValidateDataAvailability(config, triggerTimeframe, triggerCandles, sorted15m, sorted1h, sorted4h, warmupEndIndex);

        return new ReplayData
        {
            Candles15m = sorted15m,
            Candles1h = sorted1h,
            Candles4h = sorted4h,
            TriggerCandles = triggerCandles,
            TriggerTimeframe = triggerTimeframe,
            WarmupEndIndex = warmupEndIndex
        };
    }

    public static Candle? GetLatestClosedCandle(IReadOnlyList<Candle> higherTimeframeCandles, long triggerCandleOpenTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(higherTimeframeCandles);

        if (higherTimeframeCandles.Count == 0)
        {
            return null;
        }

        var intervalMs = GetIntervalMs(higherTimeframeCandles[0].Interval);
        var targetTimestamp = triggerCandleOpenTimeUtc - intervalMs;

        var low = 0;
        var high = higherTimeframeCandles.Count - 1;
        var resultIndex = -1;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);

            if (higherTimeframeCandles[mid].Timestamp <= targetTimestamp)
            {
                resultIndex = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return resultIndex >= 0 ? higherTimeframeCandles[resultIndex] : null;
    }

    private static void ValidateDataAvailability(
        BacktestConfig config,
        string triggerTimeframe,
        IReadOnlyList<Candle> triggerCandles,
        IReadOnlyList<Candle> candles15m,
        IReadOnlyList<Candle> candles1h,
        IReadOnlyList<Candle> candles4h,
        int warmupEndIndex)
    {
        if (triggerCandles.Count == 0 || warmupEndIndex >= triggerCandles.Count)
        {
            throw new NotFoundException(
                $"No candle data found for {config.Symbol}/{triggerTimeframe} between {config.StartDateUtc} and {config.EndDateUtc}");
        }

        if (candles1h.Count == 0)
        {
            throw new NotFoundException(
                $"Missing {OneHourInterval} candle data for {config.Symbol}. Cannot run backtest without higher-timeframe context.");
        }

        if (candles4h.Count == 0)
        {
            throw new NotFoundException(
                $"Missing {FourHourInterval} candle data for {config.Symbol}. Cannot run backtest without higher-timeframe context.");
        }

        if (warmupEndIndex < config.WarmupPeriod)
        {
            throw new NotFoundException(
                $"Insufficient warmup data for {config.Symbol}/{triggerTimeframe}. Need {config.WarmupPeriod} candles before start date, found {warmupEndIndex}.");
        }

        var firstEvaluationCandle = triggerCandles[warmupEndIndex];

        // Only validate context for timeframes above the trigger
        if (string.Compare(triggerTimeframe, OneHourInterval, StringComparison.OrdinalIgnoreCase) < 0
            && GetLatestClosedCandle(candles1h, firstEvaluationCandle.Timestamp) is null)
        {
            throw new NotFoundException(
                $"Missing {OneHourInterval} candle data for {config.Symbol}. Cannot run backtest without higher-timeframe context.");
        }

        if (string.Compare(triggerTimeframe, FourHourInterval, StringComparison.OrdinalIgnoreCase) < 0
            && GetLatestClosedCandle(candles4h, firstEvaluationCandle.Timestamp) is null)
        {
            throw new NotFoundException(
                $"Missing {FourHourInterval} candle data for {config.Symbol}. Cannot run backtest without higher-timeframe context.");
        }
    }

    private static IReadOnlyList<Candle> GetCandlesByTimeframe(
        string timeframe,
        IReadOnlyList<Candle> candles15m,
        IReadOnlyList<Candle> candles1h,
        IReadOnlyList<Candle> candles4h) => timeframe.ToLowerInvariant() switch
    {
        "1h" => candles1h,
        "4h" => candles4h,
        _ => candles15m
    };

    private static int DetermineWarmupEndIndex(IReadOnlyList<Candle> triggerCandles, BacktestConfig config)
    {
        for (var index = 0; index < triggerCandles.Count; index++)
        {
            if (triggerCandles[index].Timestamp >= config.StartDateUtc)
            {
                return index;
            }
        }

        return triggerCandles.Count;
    }

    private static long CalculateWarmupStartUtc(BacktestConfig config, string triggerTimeframe)
    {
        var warmupDurationUtc = checked(config.WarmupPeriod * GetIntervalMs(triggerTimeframe));
        return Math.Max(0L, config.StartDateUtc - warmupDurationUtc);
    }

    private static long CalculateHigherTimeframeQueryStartUtc(long warmupStartUtc, string interval)
    {
        var intervalMs = GetIntervalMs(interval);
        var alignedWarmupStartUtc = warmupStartUtc / intervalMs * intervalMs;
        return Math.Max(0L, alignedWarmupStartUtc - intervalMs);
    }

    private static long GetIntervalMs(string interval) => interval.ToLowerInvariant() switch
    {
        "5m" => 5L * 60L * 1000L,
        "15m" => 15L * 60L * 1000L,
        OneHourInterval => 60L * 60L * 1000L,
        FourHourInterval => 4L * 60L * 60L * 1000L,
        _ => throw new ArgumentException($"Unsupported interval: {interval}", nameof(interval))
    };

    private static List<Candle> DeduplicateByTimestamp(IReadOnlyList<Candle> candles)
    {
        return candles
            .OrderBy(c => c.Timestamp)
            .DistinctBy(c => c.Timestamp)
            .ToList();
    }
}