namespace TradePilot.Application.Webhooks.Models;

public sealed class TradingViewWebhookPayload
{
    public required string Action { get; init; }
    public string? Ticker { get; init; }
    public decimal? Contracts { get; init; }
    public decimal? Price { get; init; }
    public string? OrderType { get; init; }
    public decimal? StopLoss { get; init; }
    public decimal? TakeProfit { get; init; }
    public string? Comment { get; init; }
}