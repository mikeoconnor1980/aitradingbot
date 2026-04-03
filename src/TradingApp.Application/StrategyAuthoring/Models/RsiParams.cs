namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed record RsiParams : IEntryConditionParams
{
    public int Period { get; init; }
    public string Operator { get; init; } = string.Empty;
    public decimal Value { get; init; }
}