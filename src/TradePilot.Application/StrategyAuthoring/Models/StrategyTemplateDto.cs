namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed class StrategyTemplateDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string StrategyMode { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public string Market { get; init; } = string.Empty;
    public string[] Tags { get; init; } = [];
    public StrategyConfig Config { get; init; } = new();
    public int SortOrder { get; init; }
    public bool IsSystemTemplate { get; init; }
    public long CreatedAtUtc { get; init; }
    public long UpdatedAtUtc { get; init; }
}
