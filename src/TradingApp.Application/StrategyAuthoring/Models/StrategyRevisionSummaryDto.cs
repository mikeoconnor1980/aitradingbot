namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed class StrategyRevisionSummaryDto
{
    public int RevisionNumber { get; init; }
    public string Source { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string ChangeSummary { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}