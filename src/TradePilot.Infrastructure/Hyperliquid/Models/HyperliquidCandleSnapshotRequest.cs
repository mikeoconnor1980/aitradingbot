using System.Text.Json.Serialization;

namespace TradePilot.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidCandleSnapshotRequest
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "candleSnapshot";

    [JsonPropertyName("req")]
    public CandleSnapshotPayload Req { get; init; } = new();
}
