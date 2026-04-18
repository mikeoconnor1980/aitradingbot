namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record CandlePatternParams : IEntryConditionParams
{
    public string Pattern { get; init; } = string.Empty;
}