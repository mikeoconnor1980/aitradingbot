namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed record SourceMetadata
{
    public StrategyEntryPoint EntryPoint { get; init; }
    public string Summary { get; init; } = string.Empty;
}