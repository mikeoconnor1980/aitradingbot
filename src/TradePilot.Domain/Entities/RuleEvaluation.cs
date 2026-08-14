using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Entities;

/// <summary>Persisted evidence produced by one deterministic strategy rule.</summary>
public sealed class RuleEvaluation
{
    public Guid Id { get; private set; }
    public Guid StrategyEvaluationId { get; private set; }
    public int EvaluationOrder { get; private set; }
    public string RuleId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public RuleCategory Category { get; private set; }
    public bool Passed { get; private set; }
    public string? ActualValue { get; private set; }
    public decimal? ActualNumericValue { get; private set; }
    public string? ExpectedValue { get; private set; }
    public decimal? ExpectedNumericValue { get; private set; }
    public string? Unit { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public bool IsBlocking { get; private set; }
    public RuleEvaluationKind Kind { get; private set; }

    private RuleEvaluation()
    {
    }

    /// <summary>Creates immutable evidence for a rule that was actually evaluated.</summary>
    public static RuleEvaluation Create(
        int evaluationOrder,
        string ruleId,
        string name,
        RuleCategory category,
        bool passed,
        string reason,
        bool isBlocking,
        RuleEvaluationKind kind,
        string? actualValue = null,
        decimal? actualNumericValue = null,
        string? expectedValue = null,
        decimal? expectedNumericValue = null,
        string? unit = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(evaluationOrder);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new RuleEvaluation
        {
            Id = Guid.NewGuid(),
            EvaluationOrder = evaluationOrder,
            RuleId = ruleId.Trim(),
            Name = name.Trim(),
            Category = category,
            Passed = passed,
            ActualValue = actualValue,
            ActualNumericValue = actualNumericValue,
            ExpectedValue = expectedValue,
            ExpectedNumericValue = expectedNumericValue,
            Unit = unit,
            Reason = reason.Trim(),
            IsBlocking = isBlocking,
            Kind = kind,
        };
    }
}
