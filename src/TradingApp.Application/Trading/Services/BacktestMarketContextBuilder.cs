using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;
using TradingApp.Indicators;

namespace TradingApp.Application.Trading.Services;

public sealed class BacktestMarketContextBuilder : IMarketContextBuilder
{
    private const int FastEmaPeriod = 20;
    private const int SlowEmaPeriod = 50;
    private const int TrendEmaPeriod = 200;

    private readonly List<Candle> _candles = [];

    public void UpdateIndicators(Candle candle)
    {
        ArgumentNullException.ThrowIfNull(candle);
        _candles.Add(candle);
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

        var closes = _candles.Select(candle => candle.Close).ToList();
        var indicatorContext = BuildIndicatorContext(requiredIndicators, closes);

        return new MarketContext
        {
            Symbol = triggerCandle.Symbol,
            TimestampUtc = triggerCandle.Timestamp,
            CurrentCandle = triggerCandle,
            PreviousCandle = GetPreviousCandle(triggerCandle),
            LatestOneHourCandle = latestOneHourCandle,
            LatestFourHourCandle = latestFourHourCandle,
            Indicators = new IndicatorSnapshot
            {
                EmaFast = EmaCalculator.Calculate(closes, FastEmaPeriod) ?? 0m,
                EmaSlow = EmaCalculator.Calculate(closes, SlowEmaPeriod) ?? 0m,
                EmaTrend = latestFourHourCandle?.Close ?? EmaCalculator.Calculate(closes, TrendEmaPeriod) ?? 0m,
                Rsi = RsiCalculator.Calculate(closes, 14) ?? 50m,
                Atr = AtrCalculator.Calculate(GetBars(), 14) ?? 0m
            },
            IndicatorContext = indicatorContext
        };
    }

    private IndicatorContext? BuildIndicatorContext(
        IReadOnlyList<IndicatorRequirement>? requirements,
        IReadOnlyList<decimal> closes)
    {
        if (requirements is null || requirements.Count == 0)
        {
            return null;
        }

        var context = new IndicatorContext();
        IReadOnlyList<decimal> previousCloses = closes.Count > 1 ? closes.Take(closes.Count - 1).ToList() : [];

        foreach (var requirement in requirements)
        {
            switch (requirement.Type.ToUpperInvariant())
            {
                case "RSI":
                    context.SetRsi(
                        requirement.Period,
                        RsiCalculator.Calculate(closes, requirement.Period) ?? 50m,
                        RsiCalculator.Calculate(previousCloses, requirement.Period));
                    break;

                case "EMA":
                    context.SetEma(
                        requirement.Period,
                        EmaCalculator.Calculate(closes, requirement.Period) ?? 0m,
                        EmaCalculator.Calculate(previousCloses, requirement.Period));
                    break;

                case "SMA":
                    context.SetSma(
                        requirement.Period,
                        CalculateSma(requirement.Period),
                        CalculatePreviousSma(requirement.Period));
                    break;

                case "MACD":
                    var fast = requirement.FastPeriod ?? 12;
                    var slow = requirement.SlowPeriod ?? 26;
                    var signal = requirement.SignalPeriod ?? 9;
                    var current = MacdCalculator.Calculate(closes, fast, slow, signal);
                    var previous = MacdCalculator.Calculate(previousCloses, fast, slow, signal);

                    if (current is not null)
                    {
                        context.SetMacd(
                            fast,
                            slow,
                            signal,
                            current.Line,
                            current.Signal,
                            current.Histogram,
                            previous?.Line,
                            previous?.Signal,
                            previous?.Histogram);
                    }

                    break;
            }
        }

        return context;
    }

    private Candle? GetPreviousCandle(Candle triggerCandle)
    {
        var triggerIndex = _candles.FindLastIndex(candle =>
            candle.Timestamp == triggerCandle.Timestamp
            && string.Equals(candle.Interval, triggerCandle.Interval, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candle.Symbol, triggerCandle.Symbol, StringComparison.OrdinalIgnoreCase));

        return triggerIndex > 0 ? _candles[triggerIndex - 1] : null;
    }

    private decimal CalculateSma(int period)
    {
        if (_candles.Count == 0)
        {
            return 0m;
        }

        var startIndex = Math.Max(0, _candles.Count - period);
        var sum = 0m;
        var count = 0;

        for (var index = startIndex; index < _candles.Count; index++)
        {
            sum += _candles[index].Close;
            count++;
        }

        return sum / count;
    }

    private decimal CalculatePreviousSma(int period)
    {
        if (_candles.Count < 2)
        {
            return 0m;
        }

        var endIndex = _candles.Count - 1;
        var startIndex = Math.Max(0, endIndex - period);
        var sum = 0m;
        var count = 0;

        for (var index = startIndex; index < endIndex; index++)
        {
            sum += _candles[index].Close;
            count++;
        }

        return count > 0 ? sum / count : 0m;
    }

    private IReadOnlyList<(decimal High, decimal Low, decimal Close)> GetBars()
    {
        return _candles.Select(candle => (candle.High, candle.Low, candle.Close)).ToList();
    }
}