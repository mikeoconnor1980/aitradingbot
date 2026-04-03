namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed record RiskConfig
{
    public PositionSizeType PositionSizeType { get; init; }
    public decimal PositionSizeValue { get; init; }
    public decimal Leverage { get; init; } = 1m;
    public int MaxOpenTrades { get; init; } = 1;
    public int CooldownValue { get; init; }
    public CooldownUnit CooldownUnit { get; init; }
    public bool AllowSameCandleReentry { get; init; }
}