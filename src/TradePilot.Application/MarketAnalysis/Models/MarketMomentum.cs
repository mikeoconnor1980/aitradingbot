namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>
/// Describes momentum according to the latest 14-period RSI value.
/// </summary>
public enum MarketMomentum
{
    /// <summary>RSI is between the bearish and bullish thresholds, inclusive.</summary>
    Neutral,

    /// <summary>RSI is above the bullish threshold.</summary>
    Bullish,

    /// <summary>RSI is below the bearish threshold.</summary>
    Bearish,
}
