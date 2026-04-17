using System.Text.Json.Serialization;

namespace TradePilot.AI.Models;

internal sealed class ResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "json_object";
}