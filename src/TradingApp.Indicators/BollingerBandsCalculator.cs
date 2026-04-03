namespace TradingApp.Indicators;

/// <summary>
/// Calculates Bollinger Bands where the middle band is an SMA and the upper/lower bands
/// are offset by the configured standard-deviation multiplier.
/// Matches TradingView ta.bb() implementation.
/// </summary>
public static class BollingerBandsCalculator
{
    public static BollingerBandsResult? Calculate(
        IReadOnlyList<decimal> closes,
        int period = 20,
        decimal multiplier = 2m)
    {
        var series = CalculateSeries(closes, period, multiplier);
        return series.Count == 0 ? null : series[^1];
    }

    public static IReadOnlyList<BollingerBandsResult?> CalculateSeries(
        IReadOnlyList<decimal> closes,
        int period = 20,
        decimal multiplier = 2m)
    {
        ValidatePeriod(period);

        var result = new BollingerBandsResult?[closes.Count];

        if (closes.Count < period)
        {
            return result;
        }

        for (var lastIndex = period - 1; lastIndex < closes.Count; lastIndex++)
        {
            var startIndex = lastIndex - period + 1;
            var sum = 0m;
            for (var index = startIndex; index <= lastIndex; index++)
            {
                sum += closes[index];
            }

            var middle = sum / period;

            var sumSquaredDiff = 0m;
            for (var index = startIndex; index <= lastIndex; index++)
            {
                var diff = closes[index] - middle;
                sumSquaredDiff += diff * diff;
            }

            var standardDeviation = (decimal)Math.Sqrt((double)(sumSquaredDiff / period));
            var upper = middle + (multiplier * standardDeviation);
            var lower = middle - (multiplier * standardDeviation);

            result[lastIndex] = new BollingerBandsResult(upper, middle, lower);
        }

        return result;
    }

    private static void ValidatePeriod(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
    }
}