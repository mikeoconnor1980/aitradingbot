using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

internal sealed class HyperliquidSubscribeRequest
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "subscribe";

    [JsonPropertyName("subscription")]
    public HyperliquidSubscription Subscription { get; set; } = new();
}

internal sealed class HyperliquidSubscription
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "trades";

    [JsonPropertyName("coin")]
    public string Coin { get; set; } = string.Empty;
}