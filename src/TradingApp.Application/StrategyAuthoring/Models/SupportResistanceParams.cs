namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed record SupportResistanceParams : IEntryConditionParams
{
    public int Lookback { get; init; }
    public int Strength { get; init; } = 3;
    public string Operator { get; init; } = string.Empty;
    public decimal Tolerance { get; init; }
}
