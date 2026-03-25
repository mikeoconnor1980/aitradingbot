using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

internal sealed class HyperliquidWebSocketMessage
{
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }
}