using TradePilot.Domain.Trading;

namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record StrategyConfig : IStrategyConfig
{
    public int SchemaVersion { get; init; } = 1;
    public StrategyMode StrategyMode { get; init; }
    public string StrategyName { get; init; } = string.Empty;
    public string Exchange { get; init; } = "Hyperliquid";
    public string Market { get; init; } = string.Empty;
    public string Timeframe { get; init; } = "15m";
    public Direction Direction { get; init; }
    public bool Enabled { get; init; } = true;
    public string? TemplateId { get; init; }
    public GridConfig? Grid { get; init; }
    public TrendFilterConfig? TrendFilter { get; init; }
    public EntryLogic? EntryLogic { get; init; }
    public IReadOnlyList<EntryConditionConfig>? EntryConditions { get; init; }
    public ExitConfig Exit { get; init; } = new();
    public RiskConfig Risk { get; init; } = new();
    public StrategyMetadata? Metadata { get; init; }
    public SourceMetadata? Source { get; init; }
}