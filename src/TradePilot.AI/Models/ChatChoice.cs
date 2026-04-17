using System.Text.Json.Serialization;

namespace TradePilot.AI.Models;

internal sealed class ChatChoice
{
    [JsonPropertyName("message")]
    public ChatChoiceMessage Message { get; init; } = default!;
}