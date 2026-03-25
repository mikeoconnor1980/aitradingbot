namespace TradingApp.Application.MarketData.Models;

public sealed class MarketInfoDto
{
    public string Asset { get; init; } = string.Empty;
    public decimal MidPrice { get; init; }
    public decimal MarkPrice { get; init; }
    public decimal IndexPrice { get; init; }
    public decimal FundingRate { get; init; }
    public decimal Volume24h { get; init; }
    public decimal OpenInterest { get; init; }
    public decimal PriceChange24hPercent { get; init; }
}