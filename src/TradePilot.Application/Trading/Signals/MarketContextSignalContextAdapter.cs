using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Signals.Abstractions;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Trading.Signals;

public sealed class MarketContextSignalContextAdapter : ISignalContext
{
    private readonly MarketContext _marketContext;
    private readonly IndicatorContext? _indicatorContext;

    public MarketContextSignalContextAdapter(MarketContext marketContext)
    {
        _marketContext = marketContext ?? throw new ArgumentNullException(nameof(marketContext));
        _indicatorContext = marketContext.IndicatorContext;

        if (marketContext.CandleHistory is null)
        {
            throw new InvalidOperationException("Market context does not contain candle history for derived signal evaluation.");
        }

        var timeframe = string.IsNullOrWhiteSpace(marketContext.CurrentCandle.Interval)
            ? "trigger"
            : marketContext.CurrentCandle.Interval;

        CandlesByTimeframe = new Dictionary<string, IReadOnlyList<Candle>>(StringComparer.OrdinalIgnoreCase)
        {
            [timeframe] = marketContext.CandleHistory,
        };
    }

    public string Symbol => _marketContext.Symbol;

    public IReadOnlyDictionary<string, IReadOnlyList<Candle>> CandlesByTimeframe { get; }

    public IReadOnlyList<Candle> GetCandles(string timeframe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeframe);

        if (CandlesByTimeframe.TryGetValue(timeframe, out var candles))
        {
            return candles;
        }

        throw new KeyNotFoundException($"No candle history registered for timeframe '{timeframe}'.");
    }

    public Candle GetCurrentCandle(string timeframe)
    {
        var candles = GetCandles(timeframe);
        if (candles.Count == 0)
        {
            throw new InvalidOperationException($"No candles available for timeframe '{timeframe}'.");
        }

        return candles[^1];
    }

    public Candle? GetPreviousCandle(string timeframe, int offset = 1)
    {
        if (offset < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be at least 1.");
        }

        var candles = GetCandles(timeframe);
        return candles.Count > offset ? candles[candles.Count - 1 - offset] : null;
    }

    public decimal? GetIndicatorValue(string indicatorId)
    {
        if (_indicatorContext is null || string.IsNullOrWhiteSpace(indicatorId))
        {
            return null;
        }

        var parts = indicatorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        if (parts[0].Equals("RSI", StringComparison.OrdinalIgnoreCase) && parts.Length == 2 && int.TryParse(parts[1], out var rsiPeriod))
        {
            return _indicatorContext.GetRsi(rsiPeriod);
        }

        if (parts[0].Equals("EMA", StringComparison.OrdinalIgnoreCase) && parts.Length == 2 && int.TryParse(parts[1], out var emaPeriod))
        {
            return _indicatorContext.GetEma(emaPeriod);
        }

        if (parts[0].Equals("SMA", StringComparison.OrdinalIgnoreCase) && parts.Length == 2 && int.TryParse(parts[1], out var smaPeriod))
        {
            return _indicatorContext.GetSma(smaPeriod);
        }

        if (parts[0].Equals("SUPPORT", StringComparison.OrdinalIgnoreCase) && parts.Length == 2 && int.TryParse(parts[1], out var supportLookback))
        {
            return _indicatorContext.GetSupport(supportLookback);
        }

        if (parts[0].Equals("RESISTANCE", StringComparison.OrdinalIgnoreCase) && parts.Length == 2 && int.TryParse(parts[1], out var resistanceLookback))
        {
            return _indicatorContext.GetResistance(resistanceLookback);
        }

        if (parts[0].Equals("MACD", StringComparison.OrdinalIgnoreCase) && parts.Length is 4 or 5
            && int.TryParse(parts[1], out var fastPeriod)
            && int.TryParse(parts[2], out var slowPeriod)
            && int.TryParse(parts[3], out var signalPeriod))
        {
            if (parts.Length == 4)
            {
                return _indicatorContext.GetMacd(fastPeriod, slowPeriod, signalPeriod);
            }

            if (parts[4].Equals("signal", StringComparison.OrdinalIgnoreCase))
            {
                return _indicatorContext.GetMacdSignal(fastPeriod, slowPeriod, signalPeriod);
            }

            if (parts[4].Equals("histogram", StringComparison.OrdinalIgnoreCase))
            {
                return _indicatorContext.GetMacdHistogram(fastPeriod, slowPeriod, signalPeriod);
            }
        }

        return null;
    }

    public T? GetState<T>(string key) where T : class
    {
        return null;
    }
}