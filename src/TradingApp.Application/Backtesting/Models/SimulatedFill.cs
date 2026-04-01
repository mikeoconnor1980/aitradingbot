using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Backtesting.Models;

public sealed class SimulatedFill
{
    public required string OrderId { get; init; }
    public required long FillTimeUtc { get; init; }
    public required decimal FillPrice { get; init; }
    public required OrderSide Side { get; init; }
    public required decimal Size { get; init; }
    public required decimal Fee { get; init; }
    public required string Symbol { get; init; }
    public required TradeType TradeType { get; init; }
    public string? GridCycleId { get; init; }
    public CancellationReason? CloseReason { get; init; }
    public bool IsMaker { get; init; }
}
