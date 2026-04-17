namespace TradePilot.Application.FundingRates.Models;

public sealed class FundingRateDto
{
    public long FundingTime { get; init; }
    public decimal Rate { get; init; }
    public decimal MarkPrice { get; init; }
}