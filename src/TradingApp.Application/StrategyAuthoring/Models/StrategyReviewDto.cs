namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed class StrategyReviewDto
{
    public Guid Id { get; set; }
    public Guid StrategyId { get; set; }
    public int RevisionNumber { get; set; }
    public string ReviewMarkdown { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public long CreatedAtUtc { get; set; }
}