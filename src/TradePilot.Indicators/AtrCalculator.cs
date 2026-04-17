namespace TradePilot.Indicators;

/// <summary>
/// Calculates Average True Range (ATR) using Wilder smoothing.
/// Matches TradingView ta.atr() implementation.
/// </summary>
public static class AtrCalculator
{
    public static decimal? Calculate(IReadOnlyList<(decimal High, decimal Low, decimal Close)> bars, int period)
    {
        var series = CalculateSeries(bars, period);
        return series.Count == 0 ? null : series[^1];
    }

    public static IReadOnlyList<decimal?> CalculateSeries(IReadOnlyList<(decimal High, decimal Low, decimal Close)> bars, int period)
    {
        ValidatePeriod(period);

        var result = new decimal?[bars.Count];

        if (bars.Count < period + 1)
        {
            return result;
        }

        var trueRanges = new decimal[bars.Count - 1];
        for (var index = 1; index < bars.Count; index++)
        {
            var bar = bars[index];
            var previousClose = bars[index - 1].Close;
            trueRanges[index - 1] = Math.Max(
                bar.High - bar.Low,
                Math.Max(Math.Abs(bar.High - previousClose), Math.Abs(bar.Low - previousClose)));
        }

        var atr = 0m;
        for (var index = 0; index < period; index++)
        {
            atr += trueRanges[index];
        }

        atr /= period;
        result[period] = atr;

        for (var index = period; index < trueRanges.Length; index++)
        {
            atr = ((atr * (period - 1)) + trueRanges[index]) / period;
            result[index + 1] = atr;
        }

        return result;
    }

    private static void ValidatePeriod(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
    }
}