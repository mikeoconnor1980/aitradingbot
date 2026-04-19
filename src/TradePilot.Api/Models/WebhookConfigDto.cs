namespace TradePilot.Api.Models;

public sealed class WebhookConfigDto
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Token { get; init; }
    public string? DefaultAsset { get; init; }
    public string? TargetAgentId { get; init; }
    public required bool IsEnabled { get; init; }
    public required string CreatedAtUtc { get; init; }
    public required string UpdatedAtUtc { get; init; }
    public string? LastTriggeredAtUtc { get; init; }
}