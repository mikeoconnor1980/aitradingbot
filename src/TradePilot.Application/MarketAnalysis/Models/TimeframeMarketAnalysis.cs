namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>
/// Preserves the complete Phase 2 analysis associated with one canonical requested timeframe.
/// </summary>
/// <param name="Timeframe">The canonical requested timeframe.</param>
/// <param name="Analysis">The complete Phase 2 deterministic market analysis.</param>
public sealed record TimeframeMarketAnalysis(
    string Timeframe,
    MarketAnalysisResult Analysis);
