namespace TradePilot.Application.MarketData.Models;

public sealed class TradeTickDto
{
    public string Asset { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal Size { get; init; }
    public string Side { get; init; } = string.Empty;
    public long TimestampMs { get; init; }
}