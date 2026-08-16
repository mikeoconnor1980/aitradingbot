using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Entities;

public sealed class LiveFill
{
    public Guid Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public OrderSide Side { get; set; }
    public string Direction { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Size { get; set; }
    public decimal Fee { get; set; }
    public decimal ClosedPnl { get; set; }
    public DateTime FilledAtUtc { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid? TradeJournalRecordId { get; set; }
    public string GridCycleId { get; set; } = string.Empty;
    public string TradeType { get; set; } = string.Empty;
    public bool? IsEntry { get; set; }
}
