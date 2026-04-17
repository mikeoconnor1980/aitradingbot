using System.Text.Json.Serialization;

namespace TradePilot.AI.Models;

internal sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = default!;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; init; } = [];

    [JsonPropertyName("temperature")]
    public decimal Temperature { get; init; } = 0.1m;

    [JsonPropertyName("response_format")]
    public ResponseFormat ResponseFormat { get; init; } = new();
}