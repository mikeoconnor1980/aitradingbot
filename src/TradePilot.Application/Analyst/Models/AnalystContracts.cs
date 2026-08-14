using System.Text.Json;

namespace TradePilot.Application.Analyst.Models;

/// <summary>Represents a request to the native TradePilot Analyst.</summary>
/// <param name="Question">The user's natural-language question.</param>
/// <param name="UserId">The authenticated TradePilot user for account-scoped tools, when available.</param>
/// <param name="CorrelationId">An optional caller-supplied correlation identifier.</param>
public sealed record TradingAnalystRequest(
    string Question,
    Guid? UserId = null,
    string? CorrelationId = null);

/// <summary>Represents the outcome of one request-scoped Analyst run.</summary>
/// <param name="Response">The final safe natural-language response.</param>
/// <param name="ToolInvocations">Inspectable tool activity for tests, evaluation, and audit.</param>
/// <param name="Provider">The configured LLM provider label.</param>
/// <param name="Model">The configured model name.</param>
/// <param name="ToolRounds">The number of LLM rounds that requested tools.</param>
/// <param name="Succeeded">Whether a valid final completion was produced.</param>
/// <param name="FailureCode">A safe failure category when the run did not complete.</param>
/// <param name="Usage">Aggregated token usage when supplied by the provider.</param>
public sealed record TradingAnalystResult(
    string Response,
    IReadOnlyList<AnalystToolInvocation> ToolInvocations,
    string Provider,
    string Model,
    int ToolRounds,
    bool Succeeded,
    string? FailureCode = null,
    AnalystTokenUsage? Usage = null);

/// <summary>Records one requested Analyst tool invocation without account payloads or secrets.</summary>
/// <param name="ToolCallId">The provider's request-scoped tool call identifier.</param>
/// <param name="ToolName">The stable allow-listed tool name.</param>
/// <param name="Arguments">Sanitised, canonical tool arguments.</param>
/// <param name="Succeeded">Whether the tool produced a structured result.</param>
/// <param name="Duration">The execution duration.</param>
/// <param name="ErrorCode">A safe error category, when applicable.</param>
/// <param name="WasCached">Whether an exact request-scoped duplicate reused an earlier result.</param>
/// <param name="Result">The request-scoped structured result supplied to the LLM; never logged by the Analyst.</param>
public sealed record AnalystToolInvocation(
    string ToolCallId,
    string ToolName,
    string Arguments,
    bool Succeeded,
    TimeSpan Duration,
    string? ErrorCode = null,
    bool WasCached = false,
    JsonElement? Result = null);

/// <summary>Represents token usage returned by an Analyst provider.</summary>
public sealed record AnalystTokenUsage(int? PromptTokens, int? CompletionTokens, int? TotalTokens);

/// <summary>Defines one provider-independent function available to the Analyst LLM.</summary>
public sealed record AnalystToolDefinition(string Name, string Description, JsonElement Parameters);

/// <summary>Represents one provider-independent LLM conversation message.</summary>
public sealed record AnalystLlmMessage(
    string Role,
    string? Content = null,
    string? ToolCallId = null,
    IReadOnlyList<AnalystLlmToolCall>? ToolCalls = null);

/// <summary>Represents one structured tool request from an Analyst LLM.</summary>
public sealed record AnalystLlmToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>Represents one provider-independent Analyst completion request.</summary>
public sealed record AnalystLlmRequest(
    IReadOnlyList<AnalystLlmMessage> Messages,
    IReadOnlyList<AnalystToolDefinition> Tools);

/// <summary>Represents one provider-independent Analyst completion response.</summary>
public sealed record AnalystLlmResponse(
    string? Content,
    IReadOnlyList<AnalystLlmToolCall> ToolCalls,
    AnalystTokenUsage? Usage = null);

/// <summary>Represents a structured tool result returned to the LLM.</summary>
public sealed record AnalystToolResult(
    bool Succeeded,
    JsonElement? Result,
    AnalystToolError? Error)
{
    /// <summary>Creates a successful structured result.</summary>
    public static AnalystToolResult Success(JsonElement result) => new(true, result, null);

    /// <summary>Creates a safe structured failure result.</summary>
    public static AnalystToolResult Failure(string code, string message) =>
        new(false, null, new AnalystToolError(code, message));
}

/// <summary>Represents a safe error passed back to the LLM.</summary>
public sealed record AnalystToolError(string Code, string Message);
