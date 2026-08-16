using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Trading.Models;

/// <summary>In-process strategy result captured before signal and risk processing finalize the durable evaluation.</summary>
public sealed class StrategyEvaluationResult
{
    public bool SetupDetected { get; init; }
    public bool? TrendFilterPassed { get; init; }
    public MarketRegime? Regime { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyList<ConditionResult>? ConditionResults { get; init; }
    public IReadOnlyList<RuleEvaluationResult> Rules { get; init; } = [];
    public bool EvaluationShortCircuited { get; init; }
}

/// <summary>Typed rule evidence captured at the deterministic evaluation point before persistence mapping.</summary>
public sealed record RuleEvaluationResult(
    string RuleId,
    string Name,
    RuleCategory Category,
    bool Passed,
    string Reason,
    bool IsBlocking,
    RuleEvaluationKind Kind = RuleEvaluationKind.Blocking,
    string? ActualValue = null,
    decimal? ActualNumericValue = null,
    string? ExpectedValue = null,
    decimal? ExpectedNumericValue = null,
    string? Unit = null);

/// <summary>Risk validation output including approved signals and deterministic risk-rule evidence.</summary>
public sealed record RiskValidationResult(
    IReadOnlyList<TradingSignal> ApprovedSignals,
    IReadOnlyList<RuleEvaluationResult> Rules);
