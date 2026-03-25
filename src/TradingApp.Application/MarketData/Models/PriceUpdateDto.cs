namespace TradingApp.Application.MarketData.Models;

public sealed class PriceUpdateDto
{
    public string Asset { get; init; } = string.Empty;
    public decimal LastPrice { get; init; }
    public decimal High24h { get; init; }
    public decimal Low24h { get; init; }
    public decimal Volume24h { get; init; }
    public long Timestamp { get; init; }
}