namespace TradePilot.Application.MarketData.Models;

public sealed class ConnectionStatusDto
{
    public string Source { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Detail { get; init; }
    public int RetryCount { get; init; }
}