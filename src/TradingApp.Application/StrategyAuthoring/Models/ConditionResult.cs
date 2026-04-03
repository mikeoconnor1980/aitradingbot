namespace TradingApp.Application.StrategyAuthoring.Models;

/// <summary>
/// Result of evaluating a single entry condition.
/// </summary>
public sealed class ConditionResult
{
    public required string ConditionId { get; init; }

    public required bool Passed { get; init; }

    public required string Reason { get; init; }
}