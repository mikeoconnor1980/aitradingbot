namespace TradePilot.Domain.Entities;

public sealed class GridCycle
{
    public Guid Id { get; set; }
    public string GridCycleId { get; set; } = string.Empty;
    public string StrategyName { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public decimal AnchorPrice { get; set; }
    public int TotalLevels { get; set; }
    public int FilledLevels { get; set; }
    public string Lifecycle { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? CloseReason { get; set; }
    public decimal? RealisedPnl { get; set; }
    public string UserId { get; set; } = string.Empty;
}
