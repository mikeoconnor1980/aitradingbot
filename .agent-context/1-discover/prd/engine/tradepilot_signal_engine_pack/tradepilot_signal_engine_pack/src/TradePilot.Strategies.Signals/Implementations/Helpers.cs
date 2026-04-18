using System;
using System.Collections.Generic;
using System.Linq;
using TradePilot.Strategies.Signals.Models;

namespace TradePilot.Strategies.Signals.Implementations;

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

    public static bool GetBool(IReadOnlyDictionary<string, object?> parameters, string key, bool defaultValue)
        => parameters.TryGetValue(key, out var value) && value is not null
            ? Convert.ToBoolean(value)
            : defaultValue;
}

internal static class CandleMath
{
    public static decimal AverageRange(IReadOnlyList<Candle> candles, int lookback)
    {
        if (candles.Count == 0) return 0m;
        var take = candles.TakeLast(Math.Min(lookback, candles.Count));
        return take.Average(x => x.Range);
    }

    public static decimal Slope(IReadOnlyList<decimal> values)
    {
        if (values.Count < 2) return 0m;
        return values[^1] - values[0];
    }

    public static PivotPoint? FindRecentPivotHigh(IReadOnlyList<Candle> candles, int leftRightBars)
    {
        if (candles.Count < leftRightBars * 2 + 1) return null;

        for (var i = candles.Count - leftRightBars - 1; i >= leftRightBars; i--)
        {
            var center = candles[i].High;
            var isPivot = true;

            for (var j = i - leftRightBars; j <= i + leftRightBars; j++)
            {
                if (j == i) continue;
                if (candles[j].High >= center)
                {
                    isPivot = false;
                    break;
                }
            }

            if (isPivot)
                return new PivotPoint(i, center);
        }

        return null;
    }

    public static PivotPoint? FindRecentPivotLow(IReadOnlyList<Candle> candles, int leftRightBars)
    {
        if (candles.Count < leftRightBars * 2 + 1) return null;

        for (var i = candles.Count - leftRightBars - 1; i >= leftRightBars; i--)
        {
            var center = candles[i].Low;
            var isPivot = true;

            for (var j = i - leftRightBars; j <= i + leftRightBars; j++)
            {
                if (j == i) continue;
                if (candles[j].Low <= center)
                {
                    isPivot = false;
                    break;
                }
            }

            if (isPivot)
                return new PivotPoint(i, center);
        }

        return null;
    }

    public static int CountBoundaryTouches(
        IReadOnlyList<Candle> candles,
        decimal lowerBound,
        decimal upperBound,
        decimal tolerancePercent)
    {
        var lowerTol = lowerBound * tolerancePercent;
        var upperTol = upperBound * tolerancePercent;
        var count = 0;

        foreach (var c in candles)
        {
            var touchesLower = Math.Abs(c.Low - lowerBound) <= lowerTol;
            var touchesUpper = Math.Abs(c.High - upperBound) <= upperTol;
            if (touchesLower || touchesUpper) count++;
        }

        return count;
    }
}