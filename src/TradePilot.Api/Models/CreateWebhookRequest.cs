namespace TradePilot.Api.Models;

public sealed class CreateWebhookRequest
{
    public required string Label { get; init; }
    public string? DefaultAsset { get; init; }
    public string? TargetAgentId { get; init; }
}