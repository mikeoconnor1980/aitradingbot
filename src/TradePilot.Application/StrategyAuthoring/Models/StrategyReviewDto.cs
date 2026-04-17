namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed class StrategyReviewDto
{
    public Guid Id { get; set; }
    public Guid StrategyId { get; set; }
    public int RevisionNumber { get; set; }
    public string ReviewMarkdown { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public bool IsFallback { get; set; }
    public long CreatedAtUtc { get; set; }
}