using System.Text.Json.Serialization;

namespace TradePilot.AI.Models;

internal sealed class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<ChatChoice> Choices { get; init; } = [];
}