namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>
/// Describes whether Phase 2 volatility regimes agree across requested timeframes.
/// </summary>
public enum VolatilityAlignment
{
    /// <summary>Every requested timeframe has low volatility.</summary>
    AlignedLow,

    /// <summary>Every requested timeframe has normal volatility.</summary>
    AlignedNormal,

    /// <summary>Every requested timeframe has high volatility.</summary>
    AlignedHigh,

    /// <summary>At least two different volatility regimes occur.</summary>
    Mixed,
}
