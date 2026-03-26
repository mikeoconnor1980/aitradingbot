namespace TradingApp.Application.MarketData.Models;

/// <summary>
/// SignalR payload for order update events broadcast to Angular via ReceiveOrderUpdate.
/// </summary>
public sealed class OrderUpdateDto
{
    public DateTime Timestamp { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public string Asset { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal FilledSize { get; init; }
    public decimal RemainingSize { get; init; }
}
