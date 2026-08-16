namespace TradePilot.Application.StrategyAuthoring.Models;

/// <summary>
/// Result of evaluating a single entry condition.
/// </summary>
public sealed class ConditionResult
{
    public required string ConditionId { get; init; }

    public required bool Passed { get; init; }

    public required string Reason { get; init; }

    public decimal? Score { get; init; }

    public string? ActualValue { get; init; }

    public decimal? ActualNumericValue { get; init; }

    public string? ExpectedValue { get; init; }

    public decimal? ExpectedNumericValue { get; init; }

    public string? Unit { get; init; }

    public bool WasEvaluated { get; init; } = true;

    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
}
