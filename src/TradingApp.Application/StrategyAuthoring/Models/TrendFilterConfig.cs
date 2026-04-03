namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed record TrendFilterConfig
{
    public bool Enabled { get; init; }
    public TrendFilterType Type { get; init; }
    public int FastPeriod { get; init; }
    public int SlowPeriod { get; init; }
    public TrendOperator Operator { get; init; }
    public Direction AppliesTo { get; init; }
}