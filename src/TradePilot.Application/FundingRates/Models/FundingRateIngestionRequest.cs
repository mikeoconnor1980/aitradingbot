namespace TradePilot.Application.FundingRates.Models;

public sealed class FundingRateIngestionRequest
{
    public required string Symbol { get; init; }
    public long? StartTime { get; init; }
    public long? EndTime { get; init; }
}