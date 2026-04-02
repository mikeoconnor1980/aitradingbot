namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed record PriceVsEmaParams : IEntryConditionParams
{
    public int Period { get; init; }
    public string Operator { get; init; } = string.Empty;
}