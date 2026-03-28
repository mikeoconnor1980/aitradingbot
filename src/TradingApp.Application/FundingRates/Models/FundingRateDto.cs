namespace TradingApp.Application.FundingRates.Models;

public sealed class FundingRateDto
{
    public long FundingTime { get; init; }
    public decimal FundingRate { get; init; }
    public decimal MarkPrice { get; init; }
}