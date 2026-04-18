namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record LiquiditySweepParams : IEntryConditionParams
{
    public int LookbackBars { get; init; } = 50;

    public int PivotBars { get; init; } = 2;

    public string Side { get; init; } = string.Empty;
}