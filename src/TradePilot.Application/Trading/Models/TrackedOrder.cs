using TradePilot.Domain.Enums;

namespace TradePilot.Application.Trading.Models;

public sealed class TrackedOrder
{
    public required string OrderId { get; init; }
    public required string GridCycleId { get; init; }
    public required int Level { get; init; }
    public required string Symbol { get; init; }
    public required OrderSide Side { get; init; }
    public required decimal Price { get; init; }
    public required decimal Size { get; init; }
    public required TradeType TradeType { get; init; }
    public TradeExecutionEvidence? Evidence { get; init; }
    public DateTimeOffset PlacedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public TrackedOrderStatus Status { get; set; } = TrackedOrderStatus.Resting;
}

public enum TrackedOrderStatus
{
    Resting,
    PartiallyFilled,
    Filled,
    Cancelled,
}
