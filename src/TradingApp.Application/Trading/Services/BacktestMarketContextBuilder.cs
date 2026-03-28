using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Trading.Services;

public sealed class BacktestMarketContextBuilder : IMarketContextBuilder
{
    private readonly List<Candle> _candles = [];

    public void UpdateIndicators(Candle candle)
    {
        ArgumentNullException.ThrowIfNull(candle);
        _candles.Add(candle);
    }

    public MarketContext Build(Candle triggerCandle, Candle? latestOneHourCandle, Candle? latestFourHourCandle)
    {
        ArgumentNullException.ThrowIfNull(triggerCandle);

        return new MarketContext
        {
            Symbol = triggerCandle.Symbol,
            TimestampUtc = triggerCandle.Timestamp,
            CurrentCandle = triggerCandle,
            LatestOneHourCandle = latestOneHourCandle,
            LatestFourHourCandle = latestFourHourCandle,
            Indicators = new IndicatorSnapshot
            {
                EmaFast = CalculateEma(9),
                EmaSlow = CalculateEma(21),
                EmaTrend = latestFourHourCandle?.Close ?? CalculateEma(55),
                Rsi = CalculateRsi(14),
                Atr = CalculateAtr(14)
            }
        };
    }

    private decimal CalculateEma(int period)
    {
        if (_candles.Count == 0)
        {
            return 0m;
        }

        var closes = _candles.Select(candle => candle.Close).ToList();
        var smoothing = 2m / (period + 1m);
        var ema = closes[0];

        for (var index = 1; index < closes.Count; index++)
        {
            ema = ((closes[index] - ema) * smoothing) + ema;
        }

        return ema;
    }

    private decimal CalculateRsi(int period)
    {
        if (_candles.Count < 2)
        {
            return 50m;
        }

        var startIndex = Math.Max(1, _candles.Count - period);
        decimal gains = 0m;
        decimal losses = 0m;

        for (var index = startIndex; index < _candles.Count; index++)
        {
            var delta = _candles[index].Close - _candles[index - 1].Close;
            if (delta >= 0)
            {
                gains += delta;
            }
            else
            {
                losses += Math.Abs(delta);
            }
        }

        if (losses == 0m)
        {
            return 100m;
        }

        var relativeStrength = gains / losses;
        return 100m - (100m / (1m + relativeStrength));
    }

    private decimal CalculateAtr(int period)
    {
        if (_candles.Count == 0)
        {
            return 0m;
        }

        var startIndex = Math.Max(0, _candles.Count - period);
        decimal totalTrueRange = 0m;
        var samples = 0;

        for (var index = startIndex; index < _candles.Count; index++)
        {
            var candle = _candles[index];
            var previousClose = index == 0 ? candle.Close : _candles[index - 1].Close;
            var trueRange = Math.Max(
                candle.High - candle.Low,
                Math.Max(Math.Abs(candle.High - previousClose), Math.Abs(candle.Low - previousClose)));

            totalTrueRange += trueRange;
            samples++;
        }

        return samples == 0 ? 0m : totalTrueRange / samples;
    }
}