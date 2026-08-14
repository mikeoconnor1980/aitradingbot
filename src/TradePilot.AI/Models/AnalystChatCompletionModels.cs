using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradePilot.AI.Models;

internal sealed class AnalystChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("messages")]
    public IReadOnlyList<AnalystChatMessage> Messages { get; init; } = [];

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AnalystChatTool>? Tools { get; init; }

    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolChoice { get; init; }

    [JsonPropertyName("temperature")]
    public decimal Temperature { get; init; } = 0.1m;
}

internal sealed class AnalystChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; init; }

    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; init; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AnalystChatToolCall>? ToolCalls { get; init; }
}

internal sealed class AnalystChatTool
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public AnalystChatFunctionDefinition Function { get; init; } = new();
}

internal sealed class AnalystChatFunctionDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("parameters")]
    public JsonElement Parameters { get; init; }
}

internal sealed class AnalystChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public IReadOnlyList<AnalystChatChoice> Choices { get; init; } = [];

    [JsonPropertyName("usage")]
    public AnalystChatUsage? Usage { get; init; }
}

internal sealed class AnalystChatChoice
{
    [JsonPropertyName("message")]
    public AnalystChatMessage Message { get; init; } = new();
}

internal sealed class AnalystChatToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public AnalystChatFunctionCall Function { get; init; } = new();
}

internal sealed class AnalystChatFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; init; } = "{}";
}

internal sealed class AnalystChatUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int? PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int? CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int? TotalTokens { get; init; }
}
