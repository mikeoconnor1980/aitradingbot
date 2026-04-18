namespace TradePilot.Api.Models;

public sealed class RenameStrategyTemplateRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}