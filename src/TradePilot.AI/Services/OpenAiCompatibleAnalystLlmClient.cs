using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.AI.Models;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Analyst.Models;

namespace TradePilot.AI.Services;

/// <summary>
/// Implements the Analyst LLM abstraction over the documented OpenAI-compatible Chat Completions tool protocol.
/// </summary>
public sealed class OpenAiCompatibleAnalystLlmClient : IAnalystLlmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiCompatibleAnalystLlmClient> _logger;
    private readonly LlmOptions _options;

    /// <summary>Initializes the OpenAI-compatible Analyst client.</summary>
    public OpenAiCompatibleAnalystLlmClient(
        HttpClient httpClient,
        IOptions<LlmOptions> options,
        ILogger<OpenAiCompatibleAnalystLlmClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Provider => _options.Provider;

    /// <inheritdoc />
    public string Model => _options.ModelName;

    /// <inheritdoc />
    public async Task<AnalystLlmResponse> CompleteAsync(
        AnalystLlmRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Messages.Count == 0)
        {
            throw new ArgumentException("At least one Analyst message is required.", nameof(request));
        }

        var transportRequest = new AnalystChatCompletionRequest
        {
            Model = _options.ModelName,
            Messages = request.Messages.Select(MapMessage).ToArray(),
            Tools = request.Tools.Count == 0 ? null : request.Tools.Select(MapTool).ToArray(),
            ToolChoice = request.Tools.Count == 0 ? null : "auto",
        };

        _logger.LogInformation(
            "Sending Analyst LLM request to {Provider} model {Model} with {ToolCount} tools",
            Provider,
            Model,
            request.Tools.Count);

        using var response = await _httpClient.PostAsJsonAsync(
            "chat/completions",
            transportRequest,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Analyst LLM request failed with status code {StatusCode}",
                (int)response.StatusCode);
            throw new HttpRequestException(
                $"Analyst LLM request failed with status code {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        AnalystChatCompletionResponse? result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<AnalystChatCompletionResponse>(
                cancellationToken: cancellationToken);
        }
        catch (System.Text.Json.JsonException exception)
        {
            _logger.LogWarning(exception, "Failed to deserialize Analyst LLM response");
            throw new InvalidOperationException("Analyst LLM returned an unparseable response.", exception);
        }

        if (result?.Choices is not { Count: > 0 })
        {
            throw new InvalidOperationException("Analyst LLM returned no choices.");
        }

        var message = result.Choices[0].Message;
        var toolCalls = (message.ToolCalls ?? [])
            .Select(MapToolCall)
            .ToArray();
        if (string.IsNullOrWhiteSpace(message.Content) && toolCalls.Length == 0)
        {
            throw new InvalidOperationException("Analyst LLM returned neither content nor tool calls.");
        }

        var usage = result.Usage is null
            ? null
            : new AnalystTokenUsage(
                result.Usage.PromptTokens,
                result.Usage.CompletionTokens,
                result.Usage.TotalTokens);
        return new AnalystLlmResponse(message.Content, toolCalls, usage);
    }

    private static AnalystChatMessage MapMessage(AnalystLlmMessage message)
    {
        return new AnalystChatMessage
        {
            Role = message.Role,
            Content = message.Content,
            ToolCallId = message.ToolCallId,
            ToolCalls = message.ToolCalls?.Select(toolCall => new AnalystChatToolCall
            {
                Id = toolCall.Id,
                Function = new AnalystChatFunctionCall
                {
                    Name = toolCall.Name,
                    Arguments = toolCall.ArgumentsJson,
                },
            }).ToArray(),
        };
    }

    private static AnalystChatTool MapTool(AnalystToolDefinition tool)
    {
        return new AnalystChatTool
        {
            Function = new AnalystChatFunctionDefinition
            {
                Name = tool.Name,
                Description = tool.Description,
                Parameters = tool.Parameters,
            },
        };
    }

    private static AnalystLlmToolCall MapToolCall(AnalystChatToolCall toolCall)
    {
        if (string.IsNullOrWhiteSpace(toolCall.Id) ||
            string.IsNullOrWhiteSpace(toolCall.Function.Name) ||
            string.IsNullOrWhiteSpace(toolCall.Function.Arguments))
        {
            throw new InvalidOperationException("Analyst LLM returned a malformed tool call.");
        }

        return new AnalystLlmToolCall(
            toolCall.Id,
            toolCall.Function.Name,
            toolCall.Function.Arguments);
    }
}
