namespace TradePilot.Application.Optimization.Models;

public sealed record WalkForwardConfig
{
    public bool Enabled { get; init; }
    public decimal ValidationSplitPercent { get; init; } = 30m;
}
