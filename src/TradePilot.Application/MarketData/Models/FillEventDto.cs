namespace TradePilot.Application.MarketData.Models;

/// <summary>
/// SignalR payload for fill events broadcast to Angular via ReceiveFillEvent.
/// </summary>
public sealed class FillEventDto
{
    public DateTime Timestamp { get; init; }
    public string Asset { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public decimal Size { get; init; }
    public decimal Price { get; init; }
    public decimal Fee { get; init; }
    public decimal ClosedPnl { get; init; }
    public string OrderId { get; init; } = string.Empty;
}
