using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Backtesting.Models;

public sealed class SimulatedOrder
{
    public required string OrderId { get; init; }
    public required string Symbol { get; init; }
    public required OrderSide Side { get; init; }
    public required OrderType OrderType { get; init; }
    public required decimal Price { get; init; }
    public decimal? AnchorPrice { get; init; }
    public required decimal Size { get; init; }
    public required TradeType TradeType { get; init; }
    public string? GridCycleId { get; init; }
    public CancellationReason? CloseReason { get; init; }
    public long PlacedAtUtc { get; init; }
}
