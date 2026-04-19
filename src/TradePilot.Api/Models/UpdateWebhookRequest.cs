namespace TradePilot.Api.Models;

public sealed class UpdateWebhookRequest
{
    public required string Label { get; init; }
    public string? DefaultAsset { get; init; }
    public string? TargetAgentId { get; init; }
    public bool IsEnabled { get; init; }
}