namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>
/// Identifies a timeframe classification that differs from the longest requested reference timeframe.
/// </summary>
/// <typeparam name="TClassification">A Phase 2 classification enum.</typeparam>
/// <param name="Timeframe">The differing timeframe.</param>
/// <param name="Value">The Phase 2 value on the differing timeframe.</param>
/// <param name="ReferenceTimeframe">The longest requested timeframe.</param>
/// <param name="ReferenceValue">The Phase 2 value on the longest requested timeframe.</param>
public sealed record TimeframeClassificationConflict<TClassification>(
    string Timeframe,
    TClassification Value,
    string ReferenceTimeframe,
    TClassification ReferenceValue)
    where TClassification : struct, Enum;
