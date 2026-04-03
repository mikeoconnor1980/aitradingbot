namespace TradingApp.Indicators;

/// <summary>
/// Calculates Exponential Moving Average (EMA) using SMA-seeded initialisation.
/// Matches TradingView ta.ema() implementation.
/// </summary>
public static class EmaCalculator
{
    public static decimal? Calculate(IReadOnlyList<decimal> values, int period)
    {
        ValidatePeriod(period);

        if (values.Count < period)
        {
            return null;
        }

        var smoothing = 2m / (period + 1m);
        var ema = CalculateSeed(values, period);

        for (var index = period; index < values.Count; index++)
        {
            ema = ((values[index] - ema) * smoothing) + ema;
        }

        return ema;
    }

    public static IReadOnlyList<decimal?> CalculateSeries(IReadOnlyList<decimal> values, int period)
    {
        ValidatePeriod(period);

        var result = new decimal?[values.Count];
        if (values.Count < period)
        {
            return result;
        }

        var smoothing = 2m / (period + 1m);
        var ema = CalculateSeed(values, period);
        result[period - 1] = ema;

        for (var index = period; index < values.Count; index++)
        {
            ema = ((values[index] - ema) * smoothing) + ema;
            result[index] = ema;
        }

        return result;
    }

    private static decimal CalculateSeed(IReadOnlyList<decimal> values, int period)
    {
        var sum = 0m;
        for (var index = 0; index < period; index++)
        {
            sum += values[index];
        }

        return sum / period;
    }

    private static void ValidatePeriod(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
    }
}