namespace TradePilot.Application.Optimization.Models;

public sealed record FitnessThresholds
{
    public decimal MinWinRate { get; init; } = 40m;
    public int MinTotalTrades { get; init; } = 10;
    public decimal MaxDrawdownPercent { get; init; } = 30m;
}