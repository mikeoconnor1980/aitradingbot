namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Result of strategy evaluation that indicates whether a setup was detected.
/// </summary>
public sealed class StrategyEvaluation
{
    public bool SetupDetected { get; init; }
    public bool? TrendFilterPassed { get; init; }
    public MarketRegime? Regime { get; init; }
    public string? Reason { get; init; }
}
