using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.AI.Prompts;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Analyst.Models;

namespace TradePilot.AI.Analyst;

/// <summary>
/// Runs a bounded, request-scoped LLM tool loop over TradePilot's explicit read-only capability catalogue.
/// </summary>
public sealed class TradingAnalyst : ITradingAnalyst
{
    private const string ProviderFailureResponse =
        "I couldn't complete the analysis because the configured AI provider is currently unavailable.";
    private const string MalformedResponse =
        "I couldn't complete the analysis because the AI provider returned an invalid response.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAnalystLlmClient _llmClient;
    private readonly IAnalystToolCatalog _toolCatalog;
    private readonly LlmAnalystOptions _options;
    private readonly ILogger<TradingAnalyst> _logger;

    /// <summary>Initializes the native TradePilot Analyst.</summary>
    public TradingAnalyst(
        IAnalystLlmClient llmClient,
        IAnalystToolCatalog toolCatalog,
        IOptions<LlmAnalystOptions> options,
        ILogger<TradingAnalyst> logger)
    {
        _llmClient = llmClient;
        _toolCatalog = toolCatalog;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TradingAnalystResult> AnalyseAsync(
        TradingAnalystRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Question);

        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.CorrelationId;
        var messages = new List<AnalystLlmMessage>
        {
            new("system", TradingAnalystPrompt.SystemPrompt),
            new("user", request.Question),
        };
        var invocations = new List<AnalystToolInvocation>();
        var resultCache = new Dictionary<string, AnalystToolResult>(StringComparer.Ordinal);
        var allowedToolNames = _toolCatalog.Definitions
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.Ordinal);
        var context = new AnalystToolContext(request.UserId);
        var toolRounds = 0;
        var requestedToolCalls = 0;
        AnalystTokenUsage? usage = null;

        _logger.LogInformation(
            "TradePilot Analyst {CorrelationId} started with provider {Provider} model {Model}",
            correlationId,
            _llmClient.Provider,
            _llmClient.Model);

        for (var round = 0; round < _options.MaxToolRounds; round++)
        {
            var completion = await CompleteSafelyAsync(
                new AnalystLlmRequest(messages, _toolCatalog.Definitions),
                correlationId,
                cancellationToken);
            if (completion is null)
            {
                return CreateFailure(ProviderFailureResponse, "provider_failure", invocations, toolRounds, usage);
            }

            usage = AddUsage(usage, completion.Usage);
            if (completion.ToolCalls.Count == 0)
            {
                return CompleteWithText(completion.Content, invocations, toolRounds, usage, correlationId);
            }

            toolRounds++;
            messages.Add(new AnalystLlmMessage("assistant", completion.Content, ToolCalls: completion.ToolCalls));

            if (requestedToolCalls + completion.ToolCalls.Count > _options.MaxToolCalls)
            {
                AddLimitResults(completion.ToolCalls, messages, invocations);
                _logger.LogWarning(
                    "TradePilot Analyst {CorrelationId} reached its {MaxToolCalls} tool-call limit after {ToolRounds} rounds",
                    correlationId,
                    _options.MaxToolCalls,
                    toolRounds);
                return await CompleteAfterLimitAsync(messages, invocations, toolRounds, usage, correlationId, cancellationToken);
            }

            requestedToolCalls += completion.ToolCalls.Count;
            foreach (var toolCall in completion.ToolCalls)
            {
                var canonicalArguments = SanitizeArguments(toolCall.Name, toolCall.ArgumentsJson);
                var cacheKey = $"{toolCall.Name}\n{canonicalArguments}";
                var stopwatch = Stopwatch.StartNew();
                AnalystToolResult toolResult;
                var wasCached = resultCache.TryGetValue(cacheKey, out var cachedResult);

                if (!allowedToolNames.Contains(toolCall.Name))
                {
                    toolResult = AnalystToolResult.Failure(
                        "unknown_tool",
                        "The requested tool is not available.");
                }
                else if (wasCached)
                {
                    toolResult = cachedResult!;
                }
                else
                {
                    _logger.LogInformation(
                        "TradePilot Analyst {CorrelationId} requested tool {ToolName} with arguments {Arguments}",
                        correlationId,
                        toolCall.Name,
                        canonicalArguments);
                    toolResult = await _toolCatalog.ExecuteAsync(
                        toolCall.Name,
                        toolCall.ArgumentsJson,
                        context,
                        cancellationToken);
                    if (toolResult.Succeeded)
                    {
                        resultCache[cacheKey] = toolResult;
                    }
                }

                stopwatch.Stop();
                invocations.Add(new AnalystToolInvocation(
                    toolCall.Id,
                    toolCall.Name,
                    canonicalArguments,
                    toolResult.Succeeded,
                    stopwatch.Elapsed,
                    toolResult.Error?.Code,
                    wasCached,
                    toolResult.Result));
                messages.Add(new AnalystLlmMessage(
                    "tool",
                    JsonSerializer.Serialize(toolResult, JsonOptions),
                    toolCall.Id));

                _logger.LogInformation(
                    "TradePilot Analyst {CorrelationId} tool {ToolName} completed: Success={Succeeded}, Cached={WasCached}, DurationMs={DurationMs}",
                    correlationId,
                    toolCall.Name,
                    toolResult.Succeeded,
                    wasCached,
                    stopwatch.ElapsedMilliseconds);
            }
        }

        _logger.LogWarning(
            "TradePilot Analyst {CorrelationId} reached its {MaxToolRounds} tool-round limit",
            correlationId,
            _options.MaxToolRounds);
        return await CompleteAfterLimitAsync(messages, invocations, toolRounds, usage, correlationId, cancellationToken);
    }

    private async Task<AnalystLlmResponse?> CompleteSafelyAsync(
        AnalystLlmRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _llmClient.CompleteAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("TradePilot Analyst {CorrelationId} was cancelled", correlationId);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "TradePilot Analyst {CorrelationId} provider call failed ({ExceptionType})",
                correlationId,
                exception.GetType().Name);
            return null;
        }
    }

    private async Task<TradingAnalystResult> CompleteAfterLimitAsync(
        List<AnalystLlmMessage> messages,
        IReadOnlyList<AnalystToolInvocation> invocations,
        int toolRounds,
        AnalystTokenUsage? usage,
        string correlationId,
        CancellationToken cancellationToken)
    {
        messages.Add(new AnalystLlmMessage("system", TradingAnalystPrompt.ToolLimitPrompt));
        var completion = await CompleteSafelyAsync(
            new AnalystLlmRequest(messages, []),
            correlationId,
            cancellationToken);
        if (completion is null)
        {
            return CreateFailure(ProviderFailureResponse, "provider_failure", invocations, toolRounds, usage);
        }

        usage = AddUsage(usage, completion.Usage);
        if (completion.ToolCalls.Count > 0)
        {
            return CreateFailure(MalformedResponse, "tool_limit_exceeded", invocations, toolRounds, usage);
        }

        return CompleteWithText(completion.Content, invocations, toolRounds, usage, correlationId);
    }

    private TradingAnalystResult CompleteWithText(
        string? content,
        IReadOnlyList<AnalystToolInvocation> invocations,
        int toolRounds,
        AnalystTokenUsage? usage,
        string correlationId)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("TradePilot Analyst {CorrelationId} returned no final content", correlationId);
            return CreateFailure(MalformedResponse, "malformed_response", invocations, toolRounds, usage);
        }

        _logger.LogInformation(
            "TradePilot Analyst {CorrelationId} completed successfully after {ToolRounds} tool rounds and {ToolCalls} tool calls",
            correlationId,
            toolRounds,
            invocations.Count);
        return new TradingAnalystResult(
            content,
            invocations,
            _llmClient.Provider,
            _llmClient.Model,
            toolRounds,
            true,
            Usage: usage);
    }

    private TradingAnalystResult CreateFailure(
        string response,
        string failureCode,
        IReadOnlyList<AnalystToolInvocation> invocations,
        int toolRounds,
        AnalystTokenUsage? usage)
    {
        return new TradingAnalystResult(
            response,
            invocations,
            _llmClient.Provider,
            _llmClient.Model,
            toolRounds,
            false,
            failureCode,
            usage);
    }

    private static void AddLimitResults(
        IReadOnlyList<AnalystLlmToolCall> toolCalls,
        ICollection<AnalystLlmMessage> messages,
        ICollection<AnalystToolInvocation> invocations)
    {
        foreach (var toolCall in toolCalls)
        {
            var result = AnalystToolResult.Failure(
                "tool_limit_exceeded",
                "The request-scoped TradePilot tool limit was reached; no further tools were executed.");
            invocations.Add(new AnalystToolInvocation(
                toolCall.Id,
                toolCall.Name,
                SanitizeArguments(toolCall.Name, toolCall.ArgumentsJson),
                false,
                TimeSpan.Zero,
                result.Error!.Code));
            messages.Add(new AnalystLlmMessage(
                "tool",
                JsonSerializer.Serialize(result, JsonOptions),
                toolCall.Id));
        }
    }

    private static string SanitizeArguments(string toolName, string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return "<invalid-json>";
            }

            var allowedProperties = GetSafeArgumentNames(toolName);
            var sanitized = document.RootElement.EnumerateObject()
                .Where(property => allowedProperties.Contains(property.Name))
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToDictionary(
                    property => property.Name,
                    property => property.Value.Clone(),
                    StringComparer.Ordinal);
            return JsonSerializer.Serialize(sanitized, JsonOptions);
        }
        catch (JsonException)
        {
            return "<invalid-json>";
        }
    }

    private static IReadOnlySet<string> GetSafeArgumentNames(string toolName)
    {
        return toolName switch
        {
            "get_market_snapshot" => new HashSet<string>(["symbol", "exchange"], StringComparer.OrdinalIgnoreCase),
            "analyse_market" => new HashSet<string>(["symbol", "timeframe", "exchange", "cutoff"], StringComparer.OrdinalIgnoreCase),
            "analyse_market_multi_timeframe" => new HashSet<string>(["symbol", "timeframes", "exchange", "cutoff"], StringComparer.OrdinalIgnoreCase),
            "get_account_summary" or "get_positions" or "get_open_orders" =>
                new HashSet<string>(["exchange"], StringComparer.OrdinalIgnoreCase),
            "get_recent_fills" => new HashSet<string>(["symbol", "exchange"], StringComparer.OrdinalIgnoreCase),
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };
    }

    private static AnalystTokenUsage? AddUsage(AnalystTokenUsage? total, AnalystTokenUsage? next)
    {
        if (next is null)
        {
            return total;
        }

        return new AnalystTokenUsage(
            AddNullable(total?.PromptTokens, next.PromptTokens),
            AddNullable(total?.CompletionTokens, next.CompletionTokens),
            AddNullable(total?.TotalTokens, next.TotalTokens));
    }

    private static int? AddNullable(int? left, int? right)
    {
        return left.HasValue || right.HasValue
            ? left.GetValueOrDefault() + right.GetValueOrDefault()
            : null;
    }
}
