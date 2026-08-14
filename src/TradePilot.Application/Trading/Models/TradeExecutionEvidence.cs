using TradePilot.Domain.Enums;

namespace TradePilot.Application.Trading.Models;

/// <summary>Historical strategy and decision evidence carried from an approved signal to its exchange fill.</summary>
public sealed record TradeExecutionEvidence(
    Guid? StrategyId,
    string StrategyName,
    int? StrategyVersion,
    string ConfigurationIdentity,
    Guid? StrategyEvaluationId,
    string? MarketRegime,
    string Timeframe,
    TradeSide Side,
    decimal? Leverage,
    string SourceExchange,
    TradeExitReason? ExitReason = null);
