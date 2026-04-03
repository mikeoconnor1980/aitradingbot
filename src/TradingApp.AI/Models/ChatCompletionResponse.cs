using System.Text.Json.Serialization;

namespace TradingApp.AI.Models;

internal sealed class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<ChatChoice> Choices { get; init; } = [];
}