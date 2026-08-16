namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>
/// Describes the relationship between the two most recent confirmed swing highs and lows.
/// </summary>
public enum MarketStructure
{
    /// <summary>There are fewer than two confirmed swing highs or two confirmed swing lows.</summary>
    Unknown,

    /// <summary>The latest confirmed swing high and low are both above their predecessors.</summary>
    HigherHighHigherLow,

    /// <summary>The latest confirmed swing high and low are both below their predecessors.</summary>
    LowerHighLowerLow,

    /// <summary>The latest confirmed swing high and low equal their predecessors.</summary>
    Range,

    /// <summary>The high and low comparisons do not form a consistent trend or range.</summary>
    Mixed,
}
