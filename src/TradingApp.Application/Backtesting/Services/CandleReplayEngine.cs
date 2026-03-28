using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// Reads historical candles from the database and prepares replay data with
/// aligned higher-timeframe context for deterministic backtests.
/// </summary>
public sealed class CandleReplayEngine
{
    private const string TriggerInterval = "15m";
    private const string OneHourInterval = "1h";
    private const string FourHourInterval = "4h";

    private readonly ICandleRepository _candleRepository;

    public CandleReplayEngine(ICandleRepository candleRepository)
    {
        _candleRepository = candleRepository ?? throw new ArgumentNullException(nameof(candleRepository));
    }

    public async Task<ReplayData> LoadAsync(BacktestConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var warmupStartUtc = CalculateWarmupStartUtc(config);
        var candles15mTask = _candleRepository.GetCandlesAsync(
            config.Symbol,
            TriggerInterval,
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

        var sorted15m = candles15mTask.Result.OrderBy(candle => candle.Timestamp).ToList();
        var sorted1h = candles1hTask.Result.OrderBy(candle => candle.Timestamp).ToList();
        var sorted4h = candles4hTask.Result.OrderBy(candle => candle.Timestamp).ToList();
        var warmupEndIndex = DetermineWarmupEndIndex(sorted15m, config);

        ValidateDataAvailability(config, sorted15m, sorted1h, sorted4h, warmupEndIndex);

        return new ReplayData
        {
            Candles15m = sorted15m,
            Candles1h = sorted1h,
            Candles4h = sorted4h,
            WarmupEndIndex = warmupEndIndex
        };
    }

    public static Candle? GetLatestClosedCandle(IReadOnlyList<Candle> higherTimeframeCandles, long triggerCandleOpenTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(higherTimeframeCandles);

        Candle? latest = null;

        foreach (var candle in higherTimeframeCandles)
        {
            var closeTimeUtc = candle.Timestamp + GetIntervalMs(candle.Interval);
            if (closeTimeUtc <= triggerCandleOpenTimeUtc)
            {
                latest = candle;
                continue;
            }

            break;
        }

        return latest;
    }

    private static void ValidateDataAvailability(
        BacktestConfig config,
        IReadOnlyList<Candle> candles15m,
        IReadOnlyList<Candle> candles1h,
        IReadOnlyList<Candle> candles4h,
        int warmupEndIndex)
    {
        if (candles15m.Count == 0 || warmupEndIndex >= candles15m.Count)
        {
            throw new NotFoundException(
                $"No candle data found for {config.Symbol}/{TriggerInterval} between {config.StartDateUtc} and {config.EndDateUtc}");
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
                $"Insufficient warmup data for {config.Symbol}/{TriggerInterval}. Need {config.WarmupPeriod} candles before start date, found {warmupEndIndex}.");
        }

        var firstEvaluationCandle = candles15m[warmupEndIndex];
        if (GetLatestClosedCandle(candles1h, firstEvaluationCandle.Timestamp) is null)
        {
            throw new NotFoundException(
                $"Missing {OneHourInterval} candle data for {config.Symbol}. Cannot run backtest without higher-timeframe context.");
        }

        if (GetLatestClosedCandle(candles4h, firstEvaluationCandle.Timestamp) is null)
        {
            throw new NotFoundException(
                $"Missing {FourHourInterval} candle data for {config.Symbol}. Cannot run backtest without higher-timeframe context.");
        }
    }

    private static int DetermineWarmupEndIndex(IReadOnlyList<Candle> candles15m, BacktestConfig config)
    {
        for (var index = 0; index < candles15m.Count; index++)
        {
            if (candles15m[index].Timestamp >= config.StartDateUtc)
            {
                return index;
            }
        }

        return candles15m.Count;
    }

    private static long CalculateWarmupStartUtc(BacktestConfig config)
    {
        var warmupDurationUtc = checked(config.WarmupPeriod * GetIntervalMs(TriggerInterval));
        return Math.Max(0L, config.StartDateUtc - warmupDurationUtc);
    }

    private static long CalculateHigherTimeframeQueryStartUtc(long warmupStartUtc, string interval)
    {
        var intervalMs = GetIntervalMs(interval);
        var alignedWarmupStartUtc = warmupStartUtc / intervalMs * intervalMs;
        return Math.Max(0L, alignedWarmupStartUtc - intervalMs);
    }

    private static long GetIntervalMs(string interval) => interval switch
    {
        "5m" => 5L * 60L * 1000L,
        TriggerInterval => 15L * 60L * 1000L,
        OneHourInterval => 60L * 60L * 1000L,
        FourHourInterval => 4L * 60L * 60L * 1000L,
        _ => throw new ArgumentException($"Unsupported interval: {interval}", nameof(interval))
    };
}