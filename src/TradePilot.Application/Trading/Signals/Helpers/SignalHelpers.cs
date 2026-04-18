using TradePilot.Application.Trading.Signals.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Trading.Signals.Helpers;

internal static class SignalParameterReader
{
    public static int GetInt(IReadOnlyDictionary<string, object?> parameters, string key, int defaultValue)
        => parameters.TryGetValue(key, out var value) && value is not null
            ? Convert.ToInt32(value)
            : defaultValue;

    public static decimal GetDecimal(IReadOnlyDictionary<string, object?> parameters, string key, decimal defaultValue)
        => parameters.TryGetValue(key, out var value) && value is not null
            ? Convert.ToDecimal(value)
            : defaultValue;

    public static string GetString(IReadOnlyDictionary<string, object?> parameters, string key, string defaultValue)
        => parameters.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value) ?? defaultValue
            : defaultValue;
}

internal static class CandleMath
{
    public static decimal AverageRange(IReadOnlyList<Candle> candles, int lookback)
    {
        if (candles.Count == 0)
        {
            return 0m;
        }

        var take = candles.TakeLast(Math.Min(lookback, candles.Count));
        return take.Average(x => x.Range());
    }

    public static PivotPoint? FindRecentPivotHigh(IReadOnlyList<Candle> candles, int leftRightBars)
    {
        if (candles.Count < leftRightBars * 2 + 1)
        {
            return null;
        }

        for (var i = candles.Count - leftRightBars - 1; i >= leftRightBars; i--)
        {
            var center = candles[i].High;
            var isPivot = true;

            for (var j = i - leftRightBars; j <= i + leftRightBars; j++)
            {
                if (j == i)
                {
                    continue;
                }

                if (candles[j].High >= center)
                {
                    isPivot = false;
                    break;
                }
            }

            if (isPivot)
            {
                return new PivotPoint(i, center);
            }
        }

        return null;
    }

    public static PivotPoint? FindRecentPivotLow(IReadOnlyList<Candle> candles, int leftRightBars)
    {
        if (candles.Count < leftRightBars * 2 + 1)
        {
            return null;
        }

        for (var i = candles.Count - leftRightBars - 1; i >= leftRightBars; i--)
        {
            var center = candles[i].Low;
            var isPivot = true;

            for (var j = i - leftRightBars; j <= i + leftRightBars; j++)
            {
                if (j == i)
                {
                    continue;
                }

                if (candles[j].Low <= center)
                {
                    isPivot = false;
                    break;
                }
            }

            if (isPivot)
            {
                return new PivotPoint(i, center);
            }
        }

        return null;
    }
}
