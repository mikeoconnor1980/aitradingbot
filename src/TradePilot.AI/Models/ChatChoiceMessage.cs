using System.Text.Json.Serialization;

namespace TradePilot.AI.Models;

internal sealed class ChatChoiceMessage
{
    [JsonPropertyName("content")]
    public string Content { get; init; } = default!;
}