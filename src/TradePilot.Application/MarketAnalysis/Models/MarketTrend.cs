namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>
/// Describes strict price and EMA alignment on the analysed candle.
/// </summary>
public enum MarketTrend
{
    /// <summary>Price and the three EMAs are not strictly aligned.</summary>
    Neutral,

    /// <summary>Price is above EMA20, which is above EMA50, which is above EMA200.</summary>
    Bullish,

    /// <summary>Price is below EMA20, which is below EMA50, which is below EMA200.</summary>
    Bearish,
}
