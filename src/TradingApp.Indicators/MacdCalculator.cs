namespace TradingApp.Indicators;

/// <summary>
/// Calculates Moving Average Convergence Divergence (MACD).
/// MACD Line = EMA(fast) - EMA(slow).
/// Signal Line = EMA(signal period) of the MACD line series.
/// Histogram = MACD Line - Signal Line.
/// Matches TradingView ta.macd() implementation.
/// </summary>
public static class MacdCalculator
{
    public static MacdResult? Calculate(
        IReadOnlyList<decimal> closes,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        var series = CalculateSeries(closes, fastPeriod, slowPeriod, signalPeriod);
        return series.Count == 0 ? null : series[^1];
    }

    public static IReadOnlyList<MacdResult?> CalculateSeries(
        IReadOnlyList<decimal> closes,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        ValidatePeriod(fastPeriod);
        ValidatePeriod(slowPeriod);
        ValidatePeriod(signalPeriod);

        var result = new MacdResult?[closes.Count];

        var fastEmaSeries = EmaCalculator.CalculateSeries(closes, fastPeriod);
        var slowEmaSeries = EmaCalculator.CalculateSeries(closes, slowPeriod);

        var macdLineSeries = new List<decimal>(closes.Count);
        var macdLineIndexes = new List<int>(closes.Count);
        for (var index = 0; index < closes.Count; index++)
        {
            if (fastEmaSeries[index].HasValue && slowEmaSeries[index].HasValue)
            {
                macdLineSeries.Add(fastEmaSeries[index]!.Value - slowEmaSeries[index]!.Value);
                macdLineIndexes.Add(index);
            }
        }

        if (macdLineSeries.Count < signalPeriod)
        {
            return result;
        }

        var signalSeries = EmaCalculator.CalculateSeries(macdLineSeries, signalPeriod);
        for (var index = 0; index < signalSeries.Count; index++)
        {
            var signal = signalSeries[index];
            if (!signal.HasValue)
            {
                continue;
            }

            var line = macdLineSeries[index];
            var histogram = line - signal.Value;
            result[macdLineIndexes[index]] = new MacdResult(line, signal.Value, histogram);
        }

        return result;
    }

    private static void ValidatePeriod(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
    }
}