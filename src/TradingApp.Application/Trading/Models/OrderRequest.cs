namespace TradingApp.Application.Trading.Models;

public sealed class OrderRequest
{
    public required string Symbol { get; init; }
    public required OrderSide Side { get; init; }
    public required OrderType OrderType { get; init; }
    public required decimal Price { get; init; }
    public decimal? AnchorPrice { get; init; }
    public required decimal Size { get; init; }
    public required TradeType TradeType { get; init; }
    public string? GridCycleId { get; init; }
    public string? ClientOrderId { get; init; }
}
