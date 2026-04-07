namespace TradingApp.Domain.Entities;

public sealed class LiveFill
{
    public Guid Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Size { get; set; }
    public decimal Fee { get; set; }
    public decimal ClosedPnl { get; set; }
    public DateTime FilledAtUtc { get; set; }
    public string UserId { get; set; } = string.Empty;
}
