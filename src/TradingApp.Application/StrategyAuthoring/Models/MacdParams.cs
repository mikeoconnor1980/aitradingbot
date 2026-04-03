namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed record MacdParams : IEntryConditionParams
{
    public int FastPeriod { get; init; }
    public int SlowPeriod { get; init; }
    public int SignalPeriod { get; init; }
    public string Operator { get; init; } = string.Empty;
}