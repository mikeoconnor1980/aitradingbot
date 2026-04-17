namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record StrategyMetadata
{
    public string[] Tags { get; init; } = [];
    public string Notes { get; init; } = string.Empty;
}