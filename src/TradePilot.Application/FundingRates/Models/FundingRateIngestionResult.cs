namespace TradePilot.Application.FundingRates.Models;

public sealed class FundingRateIngestionResult
{
    public string Symbol { get; init; } = string.Empty;
    public int TotalFetched { get; init; }
    public int TotalInserted { get; init; }
    public int TotalSkipped { get; init; }
    public long ElapsedMs { get; init; }
    public string? EarliestTimestamp { get; init; }
    public string? LatestTimestamp { get; init; }
    public string? Error { get; init; }
}