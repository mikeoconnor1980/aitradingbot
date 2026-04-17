using System.Text.Json.Serialization;

namespace TradePilot.Infrastructure.Hyperliquid.Models;

public sealed class CandleSnapshotPayload
{
    [JsonPropertyName("coin")]
    public string Coin { get; init; } = string.Empty;

    [JsonPropertyName("interval")]
    public string Interval { get; init; } = string.Empty;

    [JsonPropertyName("startTime")]
    public long StartTime { get; init; }

    [JsonPropertyName("endTime")]
    public long EndTime { get; init; }
}
