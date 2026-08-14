namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>
/// Describes 14-period ATR as a percentage of the analysed candle close.
/// </summary>
public enum VolatilityRegime
{
    /// <summary>ATR percentage is below TradePilot's low-volatility threshold.</summary>
    Low,

    /// <summary>ATR percentage is between the low and high thresholds, inclusive.</summary>
    Normal,

    /// <summary>ATR percentage is above TradePilot's high-volatility threshold.</summary>
    High,
}
