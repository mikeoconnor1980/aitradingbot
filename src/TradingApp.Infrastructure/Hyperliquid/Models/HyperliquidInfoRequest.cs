using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidInfoRequest
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
}