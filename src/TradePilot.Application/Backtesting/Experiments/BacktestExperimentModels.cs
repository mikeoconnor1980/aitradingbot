using TradePilot.Application.Backtesting.Models;

namespace TradePilot.Application.Backtesting.Experiments;

public sealed record BacktestExperimentRequest(
    Guid StrategyId,
    int? StrategyVersion,
    string Symbol,
    DateTimeOffset Start,
    DateTimeOffset End,
    decimal InitialCapital,
    IReadOnlyList<BacktestCandidateRequest> Candidates,
    string UserId,
    BacktestExperimentSettings? Settings = null,
    BacktestRegimeFilter? RegimeFilter = null);

public sealed record BacktestCandidateRequest(
    string Label,
    IReadOnlyList<StrategyParameterOverride> ConfigurationOverrides);

public sealed record StrategyParameterOverride(
    string Parameter,
    string ConditionId,
    decimal Value);

public sealed record BacktestExperimentSettings(
    int WarmupPeriod = 200,
    bool EnableAuditLog = false);

public sealed record BacktestRegimeFilter(string HigherTimeframe, string Regime);

public sealed record BacktestExperimentResult(
    Guid BaseStrategyId,
    int BaseStrategyVersion,
    string Symbol,
    DateTimeOffset Start,
    DateTimeOffset End,
    decimal InitialCapital,
    BacktestExperimentMetrics Baseline,
    IReadOnlyList<BacktestCandidateExperimentResult> Candidates);

public sealed record BacktestCandidateExperimentResult(
    string Label,
    IReadOnlyList<StrategyParameterOverride> ConfigurationOverrides,
    BacktestExperimentMetrics Metrics,
    BacktestComparison Comparison);

public sealed record BacktestExperimentMetrics(
    decimal TotalPnl,
    decimal MaxDrawdownAbsolute,
    decimal MaxDrawdownPercent,
    int TotalTrades,
    decimal WinRate,
    decimal? ProfitFactor,
    decimal AverageTradePnl,
    decimal TotalFeesPaid,
    int CandlesReplayed)
{
    public static BacktestExperimentMetrics From(BacktestResult result) => new(
        result.TotalPnL,
        result.MaxDrawdownAbsolute,
        result.MaxDrawdownPercent,
        result.TotalTrades,
        result.WinRate,
        result.ProfitFactor,
        result.AverageTradePnL,
        result.TotalFeesPaid,
        result.CandlesReplayed);
}

public sealed record BacktestComparison(
    decimal TotalPnlDelta,
    decimal MaxDrawdownAbsoluteDelta,
    decimal MaxDrawdownPercentDelta,
    int TotalTradesDelta,
    decimal WinRateDelta,
    decimal? ProfitFactorDelta,
    decimal AverageTradePnlDelta,
    decimal TotalFeesPaidDelta)
{
    public static BacktestComparison Between(
        BacktestExperimentMetrics baseline,
        BacktestExperimentMetrics candidate) => new(
        candidate.TotalPnl - baseline.TotalPnl,
        candidate.MaxDrawdownAbsolute - baseline.MaxDrawdownAbsolute,
        candidate.MaxDrawdownPercent - baseline.MaxDrawdownPercent,
        candidate.TotalTrades - baseline.TotalTrades,
        candidate.WinRate - baseline.WinRate,
        Difference(candidate.ProfitFactor, baseline.ProfitFactor),
        candidate.AverageTradePnl - baseline.AverageTradePnl,
        candidate.TotalFeesPaid - baseline.TotalFeesPaid);

    private static decimal? Difference(decimal? candidate, decimal? baseline) =>
        candidate.HasValue && baseline.HasValue ? candidate.Value - baseline.Value : null;
}