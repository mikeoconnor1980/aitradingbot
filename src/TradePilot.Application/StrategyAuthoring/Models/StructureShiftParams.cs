namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record StructureShiftParams : IEntryConditionParams
{
    public int PivotBars { get; init; } = 2;

    public string Direction { get; init; } = string.Empty;
}