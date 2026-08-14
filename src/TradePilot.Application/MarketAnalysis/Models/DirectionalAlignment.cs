namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>
/// Describes cross-timeframe alignment for a bullish, bearish, or neutral Phase 2 classification.
/// </summary>
public enum DirectionalAlignment
{
    /// <summary>Every requested timeframe is bullish.</summary>
    AlignedBullish,

    /// <summary>Every requested timeframe is bearish.</summary>
    AlignedBearish,

    /// <summary>Every requested timeframe is neutral.</summary>
    AlignedNeutral,

    /// <summary>Bullish is a strict majority, no timeframe is bearish, and at least one is neutral.</summary>
    MostlyBullish,

    /// <summary>Bearish is a strict majority, no timeframe is bullish, and at least one is neutral.</summary>
    MostlyBearish,

    /// <summary>Both directions occur or no permitted strict majority exists.</summary>
    Mixed,
}
