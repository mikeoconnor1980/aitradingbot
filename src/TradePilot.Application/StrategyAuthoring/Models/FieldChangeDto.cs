namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed class FieldChangeDto
{
    public string Path { get; init; } = string.Empty;
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}