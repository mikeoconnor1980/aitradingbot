using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Entities;

public sealed class LiveOrder
{
    public Guid Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string GridCycleId { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public OrderSide Side { get; set; }
    public string OrderType { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Size { get; set; }
    public string TradeType { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime PlacedAtUtc { get; set; }
    public DateTime? FilledAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string UserId { get; set; } = string.Empty;
}
