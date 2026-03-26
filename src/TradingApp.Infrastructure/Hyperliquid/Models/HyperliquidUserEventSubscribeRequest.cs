using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Full subscribe request envelope for userEvents.
/// </summary>
internal sealed class HyperliquidUserEventSubscribeRequest
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "subscribe";

    [JsonPropertyName("subscription")]
    public HyperliquidUserEventSubscription Subscription { get; set; } = new();
}
