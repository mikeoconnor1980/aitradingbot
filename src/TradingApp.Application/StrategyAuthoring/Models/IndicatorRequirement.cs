namespace TradingApp.Application.StrategyAuthoring.Models;

/// <summary>
/// Describes an indicator that needs to be computed for strategy evaluation.
/// </summary>
public sealed record IndicatorRequirement
{
    public required string Type { get; init; }

    public int Period { get; init; }

    public int? FastPeriod { get; init; }

    public int? SlowPeriod { get; init; }

    public int? SignalPeriod { get; init; }

    public int? Lookback { get; init; }

    public int? Strength { get; init; }
}