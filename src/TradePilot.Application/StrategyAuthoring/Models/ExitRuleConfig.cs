namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record ExitRuleConfig
{
    public bool Enabled { get; init; }
    public ExitRuleType Type { get; init; }
    public decimal? Value { get; init; }
    public int? Lookback { get; init; }
    public decimal? AtrMultiplier { get; init; }
    public int? AtrPeriod { get; init; }
    public int? TrailingStopWarmup { get; init; }
}