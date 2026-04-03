namespace TradingApp.Application.StrategyAuthoring.Models;

/// <summary>
/// Overall result of evaluating all entry conditions for a signal-mode strategy.
/// </summary>
public sealed class ConditionEvaluationResult
{
    public required bool SetupDetected { get; init; }

    public bool? TrendFilterPassed { get; init; }

    public required IReadOnlyList<ConditionResult> ConditionResults { get; init; }

    public required string OverallReason { get; init; }
}