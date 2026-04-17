using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;
using TradePilot.Indicators;
using TradePilot.Indicators.Incremental;

namespace TradePilot.Application.Trading.Services;

public sealed class BacktestMarketContextBuilder : IMarketContextBuilder
{
    private const int FastEmaPeriod = 20;
    private const int SlowEmaPeriod = 50;
    private const int TrendEmaPeriod = 200;

    private readonly List<Candle> _candles = [];
    private readonly List<(decimal High, decimal Low, decimal Close)> _bars = [];

    // Fixed incremental indicators (always computed)
    private readonly IncrementalEma _emaFast = new(FastEmaPeriod);
    private readonly IncrementalEma _emaSlow = new(SlowEmaPeriod);
    private readonly IncrementalEma _emaTrend = new(TrendEmaPeriod);
    private readonly IncrementalRsi _rsi14 = new(14);
    private readonly IncrementalAtr _atr14 = new(14);

    // Dynamic incremental indicators (lazily initialized on first Build with requirements)
    private readonly Dictionary<int, IncrementalEma> _dynamicEmas = new();
    private readonly Dictionary<int, IncrementalRsi> _dynamicRsis = new();
    private readonly Dictionary<int, IncrementalSma> _dynamicSmas = new();
    private readonly Dictionary<string, IncrementalMacd> _dynamicMacds = new(StringComparer.Ordinal);

    // Previous value tracking for cross detection
    private readonly Dictionary<int, decimal?> _prevEma = new();
    private readonly Dictionary<int, decimal?> _prevRsi = new();
    private readonly Dictionary<int, decimal?> _prevSma = new();
    private readonly Dictionary<string, (decimal? Line, decimal? Signal, decimal? Histogram)> _prevMacd = new(StringComparer.Ordinal);

    // S/R previous result cache (keyed by lookback_strength)
    private readonly Dictionary<string, SupportResistanceResult?> _prevSr = new(StringComparer.Ordinal);

    // Synthetic regime provider for LLM context in backtest mode
    private readonly SyntheticRegimeProvider _syntheticRegimeProvider = new();
    private readonly int _maxLeverage;

    private bool _dynamicInitialized;

    public BacktestMarketContextBuilder(int? maxLeverage = null)
    {
        _maxLeverage = maxLeverage is > 0 ? maxLeverage.Value : LeverageCalculator.FallbackMaxLeverage;
    }

    public void UpdateIndicators(Candle candle)
    {
        ArgumentNullException.ThrowIfNull(candle);
        _candles.Add(candle);
        _bars.Add((candle.High, candle.Low, candle.Close));

        _emaFast.Add(candle.Close);
        _emaSlow.Add(candle.Close);
        _emaTrend.Add(candle.Close);
        _rsi14.Add(candle.Close);
        _atr14.Add(candle.High, candle.Low, candle.Close);
        _syntheticRegimeProvider.Update(_atr14.Current ?? 0m);

        if (_dynamicInitialized)
        {
            SnapshotDynamicPrevious();
            FeedDynamic(candle.Close);
        }
    }

    public MarketContext Build(Candle triggerCandle, Candle? latestOneHourCandle, Candle? latestFourHourCandle)
    {
        return Build(triggerCandle, latestOneHourCandle, latestFourHourCandle, null);
    }

    public MarketContext Build(
        Candle triggerCandle,
        Candle? latestOneHourCandle,
        Candle? latestFourHourCandle,
        IReadOnlyList<IndicatorRequirement>? requiredIndicators)
    {
        ArgumentNullException.ThrowIfNull(triggerCandle);

        var indicatorContext = BuildIndicatorContext(requiredIndicators);

        var indicators = new IndicatorSnapshot
        {
            EmaFast = _emaFast.Current ?? 0m,
            EmaSlow = _emaSlow.Current ?? 0m,
            EmaTrend = latestFourHourCandle?.Close ?? _emaTrend.Current ?? 0m,
            Rsi = _rsi14.Current ?? 50m,
            Atr = _atr14.Current ?? 0m
        };

        var llmContext = _syntheticRegimeProvider.Evaluate(indicators, triggerCandle.Timestamp);

        return new MarketContext
        {
            Symbol = triggerCandle.Symbol,
            TimestampUtc = triggerCandle.Timestamp,
            CurrentCandle = triggerCandle,
            PreviousCandle = GetPreviousCandle(triggerCandle),
            LatestOneHourCandle = latestOneHourCandle,
            LatestFourHourCandle = latestFourHourCandle,
            Indicators = indicators,
            IndicatorContext = indicatorContext,
            LlmContext = llmContext,
            MaxLeverage = _maxLeverage
        };
    }

    private IndicatorContext? BuildIndicatorContext(IReadOnlyList<IndicatorRequirement>? requirements)
    {
        if (requirements is null || requirements.Count == 0)
        {
            return null;
        }

        EnsureDynamicInitialized(requirements);

        var context = new IndicatorContext();

        foreach (var requirement in requirements)
        {
            switch (requirement.Type.ToUpperInvariant())
            {
                case "RSI":
                    if (_dynamicRsis.TryGetValue(requirement.Period, out var rsi))
                    {
                        _prevRsi.TryGetValue(requirement.Period, out var prevRsi);
                        context.SetRsi(requirement.Period, rsi.Current ?? 50m, prevRsi);
                    }

                    break;

                case "EMA":
                    if (_dynamicEmas.TryGetValue(requirement.Period, out var ema))
                    {
                        _prevEma.TryGetValue(requirement.Period, out var prevEma);
                        context.SetEma(requirement.Period, ema.Current ?? 0m, prevEma);
                    }

                    break;

                case "SMA":
                    if (_dynamicSmas.TryGetValue(requirement.Period, out var sma))
                    {
                        _prevSma.TryGetValue(requirement.Period, out var prevSma);
                        context.SetSma(requirement.Period, sma.Current, prevSma);
                    }

                    break;

                case "MACD":
                {
                    var fast = requirement.FastPeriod ?? 12;
                    var slow = requirement.SlowPeriod ?? 26;
                    var signal = requirement.SignalPeriod ?? 9;
                    var key = MacdKey(fast, slow, signal);

                    if (_dynamicMacds.TryGetValue(key, out var macd) && macd.Line.HasValue)
                    {
                        _prevMacd.TryGetValue(key, out var prev);
                        context.SetMacd(
                            fast, slow, signal,
                            macd.Line.Value,
                            macd.Signal ?? 0m,
                            macd.Histogram ?? 0m,
                            prev.Line,
                            prev.Signal,
                            prev.Histogram);
                    }

                    break;
                }

                case "SUPPORT_RESISTANCE":
                {
                    var lookback = requirement.Lookback ?? 50;
                    var strength = requirement.Strength ?? 3;
                    var srKey = $"{lookback}_{strength}";
                    var srResult = SupportResistanceCalculator.Calculate(_bars, lookback, strength);
                    _prevSr.TryGetValue(srKey, out var previousSrResult);
                    _prevSr[srKey] = srResult;

                    if (srResult?.Support.HasValue == true)
                    {
                        context.SetSupport(lookback, srResult.Support.Value, previousSrResult?.Support);
                    }

                    if (srResult?.Resistance.HasValue == true)
                    {
                        context.SetResistance(lookback, srResult.Resistance.Value, previousSrResult?.Resistance);
                    }

                    break;
                }
            }
        }

        return context;
    }

    private void EnsureDynamicInitialized(IReadOnlyList<IndicatorRequirement> requirements)
    {
        if (_dynamicInitialized)
        {
            return;
        }

        foreach (var req in requirements)
        {
            switch (req.Type.ToUpperInvariant())
            {
                case "RSI":
                    _dynamicRsis.TryAdd(req.Period, new IncrementalRsi(req.Period));
                    break;
                case "EMA":
                    _dynamicEmas.TryAdd(req.Period, new IncrementalEma(req.Period));
                    break;
                case "SMA":
                    _dynamicSmas.TryAdd(req.Period, new IncrementalSma(req.Period));
                    break;
                case "MACD":
                {
                    var key = MacdKey(req.FastPeriod ?? 12, req.SlowPeriod ?? 26, req.SignalPeriod ?? 9);
                    _dynamicMacds.TryAdd(key, new IncrementalMacd(req.FastPeriod ?? 12, req.SlowPeriod ?? 26, req.SignalPeriod ?? 9));
                    break;
                }
            }
        }

        foreach (var candle in _candles)
        {
            SnapshotDynamicPrevious();
            FeedDynamic(candle.Close);
        }

        _dynamicInitialized = true;
    }

    private void SnapshotDynamicPrevious()
    {
        foreach (var (period, ema) in _dynamicEmas)
        {
            _prevEma[period] = ema.Current;
        }

        foreach (var (period, rsi) in _dynamicRsis)
        {
            _prevRsi[period] = rsi.Current;
        }

        foreach (var (period, sma) in _dynamicSmas)
        {
            _prevSma[period] = sma.Current;
        }

        foreach (var (key, macd) in _dynamicMacds)
        {
            _prevMacd[key] = (macd.Line, macd.Signal, macd.Histogram);
        }
    }

    private void FeedDynamic(decimal close)
    {
        foreach (var (_, ema) in _dynamicEmas)
        {
            ema.Add(close);
        }

        foreach (var (_, rsi) in _dynamicRsis)
        {
            rsi.Add(close);
        }

        foreach (var (_, sma) in _dynamicSmas)
        {
            sma.Add(close);
        }

        foreach (var (_, macd) in _dynamicMacds)
        {
            macd.Add(close);
        }
    }

    private Candle? GetPreviousCandle(Candle triggerCandle)
    {
        var triggerIndex = _candles.FindLastIndex(candle =>
            candle.Timestamp == triggerCandle.Timestamp
            && string.Equals(candle.Interval, triggerCandle.Interval, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candle.Symbol, triggerCandle.Symbol, StringComparison.OrdinalIgnoreCase));

        return triggerIndex > 0 ? _candles[triggerIndex - 1] : null;
    }

    private static string MacdKey(int fast, int slow, int signal) => $"{fast}_{slow}_{signal}";
}