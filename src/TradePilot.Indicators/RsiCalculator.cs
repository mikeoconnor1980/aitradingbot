namespace TradePilot.Indicators;

/// <summary>
/// Calculates Relative Strength Index (RSI) using Wilder smoothing.
/// Matches TradingView ta.rsi() implementation.
/// </summary>
public static class RsiCalculator
{
    public static decimal? Calculate(IReadOnlyList<decimal> closes, int period)
    {
        var series = CalculateSeries(closes, period);
        return series.Count == 0 ? null : series[^1];
    }

    public static IReadOnlyList<decimal?> CalculateSeries(IReadOnlyList<decimal> closes, int period)
    {
        ValidatePeriod(period);

        var result = new decimal?[closes.Count];

        if (closes.Count < period + 1)
        {
            return result;
        }

        var deltas = new decimal[closes.Count - 1];
        for (var index = 0; index < deltas.Length; index++)
        {
            deltas[index] = closes[index + 1] - closes[index];
        }

        decimal averageGain = 0m;
        decimal averageLoss = 0m;

        for (var index = 0; index < period; index++)
        {
            if (deltas[index] >= 0m)
            {
                averageGain += deltas[index];
            }
            else
            {
                averageLoss += Math.Abs(deltas[index]);
            }
        }

        averageGain /= period;
        averageLoss /= period;

        result[period] = CalculateRsiValue(averageGain, averageLoss);

        for (var index = period; index < deltas.Length; index++)
        {
            var gain = deltas[index] >= 0m ? deltas[index] : 0m;
            var loss = deltas[index] < 0m ? Math.Abs(deltas[index]) : 0m;

            averageGain = ((averageGain * (period - 1)) + gain) / period;
            averageLoss = ((averageLoss * (period - 1)) + loss) / period;

            result[index + 1] = CalculateRsiValue(averageGain, averageLoss);
        }

        return result;
    }

    private static decimal CalculateRsiValue(decimal averageGain, decimal averageLoss)
    {
        if (averageGain == 0m && averageLoss == 0m)
        {
            return 50m;
        }

        if (averageLoss == 0m)
        {
            return 100m;
        }

        var relativeStrength = averageGain / averageLoss;
        return 100m - (100m / (1m + relativeStrength));
    }

    private static void ValidatePeriod(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
    }
}