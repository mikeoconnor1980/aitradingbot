namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>
/// Describes cross-timeframe alignment of the existing Phase 2 market-structure classification.
/// </summary>
public enum StructureAlignment
{
    /// <summary>Every timeframe has higher-high/higher-low structure.</summary>
    AlignedHigherHighHigherLow,

    /// <summary>Every timeframe has lower-high/lower-low structure.</summary>
    AlignedLowerHighLowerLow,

    /// <summary>Every timeframe is classified as range.</summary>
    AlignedRange,

    /// <summary>Every timeframe is classified as mixed.</summary>
    AlignedMixed,

    /// <summary>Every timeframe has unknown structure.</summary>
    AlignedUnknown,

    /// <summary>Higher-high/higher-low is a strict majority and no timeframe has bearish structure.</summary>
    MostlyBullish,

    /// <summary>Lower-high/lower-low is a strict majority and no timeframe has bullish structure.</summary>
    MostlyBearish,

    /// <summary>Directional structures conflict or no permitted strict majority exists.</summary>
    Mixed,
}
