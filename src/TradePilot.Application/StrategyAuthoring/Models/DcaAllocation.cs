namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record DcaAllocation
{
    public string Market { get; init; } = string.Empty;
    public decimal WeightPercent { get; init; }
}