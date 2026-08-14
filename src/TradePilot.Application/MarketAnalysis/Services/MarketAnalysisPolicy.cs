using TradePilot.Application.MarketAnalysis.Models;

namespace TradePilot.Application.MarketAnalysis.Services;

internal static class MarketAnalysisPolicy
{
    public const decimal BearishMomentumThreshold = 45m;
    public const decimal BullishMomentumThreshold = 55m;
    public const decimal LowVolatilityThresholdPercent = 1m;
    public const decimal HighVolatilityThresholdPercent = 3m;

    /// <summary>
    /// Classifies trend from strict close and EMA alignment.
    /// </summary>
    public static MarketTrend ClassifyTrend(decimal price, decimal ema20, decimal ema50, decimal ema200)
    {
        if (price > ema20 && ema20 > ema50 && ema50 > ema200)
        {
            return MarketTrend.Bullish;
        }

        if (price < ema20 && ema20 < ema50 && ema50 < ema200)
        {
            return MarketTrend.Bearish;
        }

        return MarketTrend.Neutral;
    }

    /// <summary>
    /// Classifies momentum using the named RSI thresholds.
    /// </summary>
    public static MarketMomentum ClassifyMomentum(decimal rsi)
    {
        return rsi switch
        {
            > BullishMomentumThreshold => MarketMomentum.Bullish,
            < BearishMomentumThreshold => MarketMomentum.Bearish,
            _ => MarketMomentum.Neutral,
        };
    }

    /// <summary>
    /// Classifies normalized ATR using the named percentage thresholds.
    /// </summary>
    public static VolatilityRegime ClassifyVolatility(decimal atrPercent)
    {
        return atrPercent switch
        {
            < LowVolatilityThresholdPercent => VolatilityRegime.Low,
            > HighVolatilityThresholdPercent => VolatilityRegime.High,
            _ => VolatilityRegime.Normal,
        };
    }

    /// <summary>
    /// Classifies the relationship between the two latest confirmed highs and lows.
    /// </summary>
    public static MarketStructure ClassifyStructure(ConfirmedSwings swings)
    {
        if (swings.Highs.Count < 2 || swings.Lows.Count < 2)
        {
            return MarketStructure.Unknown;
        }

        var previousHigh = swings.Highs[^2];
        var latestHigh = swings.Highs[^1];
        var previousLow = swings.Lows[^2];
        var latestLow = swings.Lows[^1];

        if (latestHigh > previousHigh && latestLow > previousLow)
        {
            return MarketStructure.HigherHighHigherLow;
        }

        if (latestHigh < previousHigh && latestLow < previousLow)
        {
            return MarketStructure.LowerHighLowerLow;
        }

        if (latestHigh == previousHigh && latestLow == previousLow)
        {
            return MarketStructure.Range;
        }

        return MarketStructure.Mixed;
    }
}
