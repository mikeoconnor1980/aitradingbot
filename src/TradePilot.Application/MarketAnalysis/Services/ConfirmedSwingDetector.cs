using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.MarketAnalysis.Services;

internal static class ConfirmedSwingDetector
{
    public const int PivotWindow = 2;

    /// <summary>
    /// Finds strict pivots with two completed candles on each side of every candidate.
    /// </summary>
    public static ConfirmedSwings Detect(IReadOnlyList<CandleDto> candles)
    {
        var highs = new List<decimal>();
        var lows = new List<decimal>();

        for (var index = PivotWindow; index < candles.Count - PivotWindow; index++)
        {
            var candidate = candles[index];
            var isSwingHigh = true;
            var isSwingLow = true;

            for (var offset = 1; offset <= PivotWindow; offset++)
            {
                isSwingHigh &= candidate.High > candles[index - offset].High
                    && candidate.High > candles[index + offset].High;
                isSwingLow &= candidate.Low < candles[index - offset].Low
                    && candidate.Low < candles[index + offset].Low;
            }

            if (isSwingHigh)
            {
                highs.Add(candidate.High);
            }

            if (isSwingLow)
            {
                lows.Add(candidate.Low);
            }
        }

        return new ConfirmedSwings(highs, lows);
    }
}
