namespace TradePilot.Domain.Enums;

/// <summary>How a rule result contributes to the strategy decision.</summary>
public enum RuleEvaluationKind
{
    Blocking,
    Informational,
    RiskOverride,
}
