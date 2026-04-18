namespace TradePilot.Api.Models;

public sealed class PromoteStrategyTemplateRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string[] Tags { get; init; } = [];
}