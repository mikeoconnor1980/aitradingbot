using TradePilot.Domain.Entities;

namespace TradePilot.Application.StrategyEvaluations.Models;

/// <summary>Bounded filters shared by strategy-evaluation persistence queries.</summary>
public sealed record StrategyEvaluationFilter(
    Guid? StrategyId = null,
    string? StrategyName = null,
    int? StrategyVersion = null,
    string? Symbol = null,
    long? FromUtc = null,
    long? ToUtc = null);

/// <summary>Deterministic count of evaluations blocked by one stable rule ID.</summary>
public sealed record RuleFailureCount(string RuleId, string RuleName, int Count);

/// <summary>Database-calculated strategy-evaluation facts for a bounded period.</summary>
public sealed record StrategyEvaluationSummary(
    int TotalEvaluations,
    int CandidateEvaluations,
    int TradeDecisions,
    int NoTradeDecisions,
    int RiskRejectedDecisions,
    IReadOnlyList<RuleFailureCount> RuleFailureCounts,
    RuleFailureCount? MostCommonBlockingRule);

/// <summary>Result returned when querying a bounded strategy-evaluation history.</summary>
public sealed record StrategyEvaluationsResult(IReadOnlyList<StrategyEvaluation> Evaluations, int Limit);
