using System.Text.Json.Serialization;

namespace TradePilot.Infrastructure.Services;

internal sealed class FearGreedApiResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<FearGreedApiDataItem> Data { get; set; } = [];

    [JsonPropertyName("metadata")]
    public FearGreedApiMetadata Metadata { get; set; } = new();
}

internal sealed class FearGreedApiDataItem
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("value_classification")]
    public string ValueClassification { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("time_until_update")]
    public string? TimeUntilUpdate { get; set; }
}

internal sealed class FearGreedApiMetadata
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
