namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>
/// Contains typed cross-timeframe differences anchored to the longest requested timeframe.
/// </summary>
/// <param name="PrimaryVsShortTermTrendConflict">Whether the shortest and longest timeframe trends differ.</param>
/// <param name="BullishAndBearishTrendsPresent">Whether bullish and bearish Phase 2 trends both occur.</param>
/// <param name="Trends">Trend values that differ from the longest timeframe trend.</param>
/// <param name="Momentum">Momentum values that differ from the longest timeframe momentum.</param>
/// <param name="MarketStructures">Structure values that differ from the longest timeframe structure.</param>
/// <param name="VolatilityRegimes">Volatility values that differ from the longest timeframe regime.</param>
public sealed record MultiTimeframeMarketAnalysisConflicts(
    bool PrimaryVsShortTermTrendConflict,
    bool BullishAndBearishTrendsPresent,
    IReadOnlyList<TimeframeClassificationConflict<MarketTrend>> Trends,
    IReadOnlyList<TimeframeClassificationConflict<MarketMomentum>> Momentum,
    IReadOnlyList<TimeframeClassificationConflict<MarketStructure>> MarketStructures,
    IReadOnlyList<TimeframeClassificationConflict<VolatilityRegime>> VolatilityRegimes)
{
    /// <summary>Gets whether at least one trend differs from the longest timeframe trend.</summary>
    public bool HasTrendConflict => Trends.Count > 0;

    /// <summary>Gets whether at least one momentum value differs from the longest timeframe value.</summary>
    public bool HasMomentumConflict => Momentum.Count > 0;

    /// <summary>Gets whether at least one structure differs from the longest timeframe structure.</summary>
    public bool HasMarketStructureConflict => MarketStructures.Count > 0;

    /// <summary>Gets whether at least one volatility regime differs from the longest timeframe regime.</summary>
    public bool HasVolatilityConflict => VolatilityRegimes.Count > 0;
}
