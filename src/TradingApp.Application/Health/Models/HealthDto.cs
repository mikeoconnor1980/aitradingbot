namespace TradingApp.Application.Health.Models;

public sealed class HealthDto
{
    public string Status { get; set; } = string.Empty;
    public string WalletAddress { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string? Error { get; set; }
}
