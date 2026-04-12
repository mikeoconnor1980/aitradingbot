namespace TradingApp.Application.StrategyAuthoring.Models;

/// <summary>
/// Runtime risk-management limits enforced by the LiveRiskEngine.
/// Bound from configuration section "RiskLimits".
/// </summary>
public sealed record RiskLimitsConfig
{
    public const string SectionName = "RiskLimits";

    /// <summary>Maximum USD loss allowed in a rolling 24-hour window before the circuit breaker trips.</summary>
    public decimal MaxDailyLossUsd { get; init; } = 500m;

    /// <summary>Maximum number of open orders allowed at any time. Signals that exceed this are blocked.</summary>
    public int MaxOpenOrders { get; init; } = 20;

    /// <summary>Maximum notional size (USD) of any single order.</summary>
    public decimal MaxOrderSizeUsd { get; init; } = 10_000m;

    /// <summary>
    /// When the circuit breaker trips, how long (minutes) before it auto-resets.
    /// 0 = manual reset only (via service restart).
    /// </summary>
    public int CircuitBreakerCooldownMinutes { get; init; } = 60;

    /// <summary>
    /// Maximum portfolio heat (aggregate risk) as a percentage of equity.
    /// Heat = sum of R (risk in USD) across all open positions / equity x 100.
    /// 0 = disabled (no heat limit enforced).
    /// </summary>
    public decimal MaxPortfolioHeatPercent { get; init; } = 6m;
}
