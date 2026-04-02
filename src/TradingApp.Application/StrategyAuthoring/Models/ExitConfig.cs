namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed record ExitConfig
{
    public ExitRuleConfig TakeProfit { get; init; } = new();
    public ExitRuleConfig StopLoss { get; init; } = new();
    public bool ExitOnOppositeSignal { get; init; }
}