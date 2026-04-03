namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed class StrategySummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Market { get; init; } = string.Empty;
    public string Timeframe { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public string StrategyMode { get; init; } = string.Empty;
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}