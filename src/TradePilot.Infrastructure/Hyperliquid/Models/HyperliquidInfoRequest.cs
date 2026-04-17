using System.Text.Json.Serialization;

namespace TradePilot.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidInfoRequest
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
}