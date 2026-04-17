namespace TradePilot.Indicators;

/// <summary>
/// Identifies support and resistance levels using swing-point detection.
/// A swing low is a candle low that is lower than the lows of <c>strength</c> candles on either side.
/// A swing high is a candle high that is higher than the highs of <c>strength</c> candles on either side.
/// The nearest support is the highest swing low below the current price.
/// The nearest resistance is the lowest swing high above the current price.
/// </summary>
public static class SupportResistanceCalculator
{
    private const int DefaultStrength = 3;

    /// <summary>
    /// Finds the nearest support and resistance levels relative to the current price.
    /// </summary>
    /// <param name="bars">OHLC bar data in chronological order.</param>
    /// <param name="lookback">Number of historical bars to scan for swing points.</param>
    /// <param name="strength">
    /// Number of bars on each side a swing point must dominate.
    /// Higher values find stronger (more significant) levels.
    /// </param>
    /// <returns>The nearest support and resistance levels, or null if not enough data.</returns>
    public static SupportResistanceResult? Calculate(
        IReadOnlyList<(decimal High, decimal Low, decimal Close)> bars,
        int lookback,
        int strength = DefaultStrength)
    {
        ValidateParameters(lookback, strength);

        if (bars.Count < (2 * strength) + 1)
        {
            return null;
        }

        var currentPrice = bars[^1].Close;
        var startIndex = Math.Max(strength, bars.Count - lookback);
        var endIndex = bars.Count - 1 - strength;

        decimal? nearestSupport = null;
        decimal? nearestResistance = null;

        for (var index = startIndex; index <= endIndex; index++)
        {
            if (IsSwingLow(bars, index, strength))
            {
                var swingLow = bars[index].Low;
                if (swingLow < currentPrice)
                {
                    if (!nearestSupport.HasValue || swingLow > nearestSupport.Value)
                    {
                        nearestSupport = swingLow;
                    }
                }
            }

            if (IsSwingHigh(bars, index, strength))
            {
                var swingHigh = bars[index].High;
                if (swingHigh > currentPrice)
                {
                    if (!nearestResistance.HasValue || swingHigh < nearestResistance.Value)
                    {
                        nearestResistance = swingHigh;
                    }
                }
            }
        }

        return new SupportResistanceResult(nearestSupport, nearestResistance);
    }

    private static bool IsSwingLow(
        IReadOnlyList<(decimal High, decimal Low, decimal Close)> bars,
        int index,
        int strength)
    {
        var candidateLow = bars[index].Low;

        for (var offset = 1; offset <= strength; offset++)
        {
            if (bars[index - offset].Low <= candidateLow || bars[index + offset].Low <= candidateLow)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSwingHigh(
        IReadOnlyList<(decimal High, decimal Low, decimal Close)> bars,
        int index,
        int strength)
    {
        var candidateHigh = bars[index].High;

        for (var offset = 1; offset <= strength; offset++)
        {
            if (bars[index - offset].High >= candidateHigh || bars[index + offset].High >= candidateHigh)
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateParameters(int lookback, int strength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lookback);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strength);
    }
}
