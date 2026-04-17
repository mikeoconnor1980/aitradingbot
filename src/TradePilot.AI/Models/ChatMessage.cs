using System.Text.Json.Serialization;

namespace TradePilot.AI.Models;

internal sealed class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = default!;

    [JsonPropertyName("content")]
    public string Content { get; init; } = default!;
}