namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed class StrategyDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string StrategyType { get; init; } = string.Empty;
    public StrategyConfig Config { get; init; } = new();
    public bool IsRunning { get; init; }
    public string? AssignedAgentId { get; init; }
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}