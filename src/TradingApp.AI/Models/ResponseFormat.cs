using System.Text.Json.Serialization;

namespace TradingApp.AI.Models;

internal sealed class ResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "json_object";
}