namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>
/// Represents deterministic composition of complete Phase 2 analyses across multiple timeframes.
/// </summary>
/// <param name="Symbol">The requested exchange-facing symbol.</param>
/// <param name="GeneratedAt">The UTC instant when the composite result was completed.</param>
/// <param name="Timeframes">Complete Phase 2 evidence ordered from shortest to longest timeframe.</param>
/// <param name="PrimaryTimeframe">The longest requested timeframe.</param>
/// <param name="ShortTermTimeframe">The shortest requested timeframe.</param>
/// <param name="PrimaryTrend">The unchanged Phase 2 trend from the longest timeframe.</param>
/// <param name="ShortTermTrend">The unchanged Phase 2 trend from the shortest timeframe.</param>
/// <param name="TrendAlignment">Cross-timeframe alignment of Phase 2 trends.</param>
/// <param name="MomentumAlignment">Cross-timeframe alignment of Phase 2 momentum values.</param>
/// <param name="StructureAlignment">Cross-timeframe alignment of Phase 2 market structures.</param>
/// <param name="VolatilityAlignment">Cross-timeframe alignment of Phase 2 volatility regimes.</param>
/// <param name="BullishTrendCount">Number of bullish Phase 2 trends.</param>
/// <param name="BearishTrendCount">Number of bearish Phase 2 trends.</param>
/// <param name="NeutralTrendCount">Number of neutral Phase 2 trends.</param>
/// <param name="BullishMomentumCount">Number of bullish Phase 2 momentum values.</param>
/// <param name="BearishMomentumCount">Number of bearish Phase 2 momentum values.</param>
/// <param name="NeutralMomentumCount">Number of neutral Phase 2 momentum values.</param>
/// <param name="HigherHighHigherLowStructureCount">Number of bullish Phase 2 structures.</param>
/// <param name="LowerHighLowerLowStructureCount">Number of bearish Phase 2 structures.</param>
/// <param name="RangeStructureCount">Number of Phase 2 range structures.</param>
/// <param name="MixedStructureCount">Number of Phase 2 mixed structures.</param>
/// <param name="UnknownStructureCount">Number of Phase 2 unknown structures.</param>
/// <param name="LowVolatilityCount">Number of low Phase 2 volatility regimes.</param>
/// <param name="NormalVolatilityCount">Number of normal Phase 2 volatility regimes.</param>
/// <param name="HighVolatilityCount">Number of high Phase 2 volatility regimes.</param>
/// <param name="Conflicts">Typed differences from the longest timeframe classifications.</param>
public sealed record MultiTimeframeMarketAnalysisResult(
    string Symbol,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<TimeframeMarketAnalysis> Timeframes,
    string PrimaryTimeframe,
    string ShortTermTimeframe,
    MarketTrend PrimaryTrend,
    MarketTrend ShortTermTrend,
    DirectionalAlignment TrendAlignment,
    DirectionalAlignment MomentumAlignment,
    StructureAlignment StructureAlignment,
    VolatilityAlignment VolatilityAlignment,
    int BullishTrendCount,
    int BearishTrendCount,
    int NeutralTrendCount,
    int BullishMomentumCount,
    int BearishMomentumCount,
    int NeutralMomentumCount,
    int HigherHighHigherLowStructureCount,
    int LowerHighLowerLowStructureCount,
    int RangeStructureCount,
    int MixedStructureCount,
    int UnknownStructureCount,
    int LowVolatilityCount,
    int NormalVolatilityCount,
    int HighVolatilityCount,
    MultiTimeframeMarketAnalysisConflicts Conflicts)
{
    /// <summary>Gets the number of distinct requested timeframes.</summary>
    public int TotalTimeframes => Timeframes.Count;
}
