using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingApp.AI.Prompts;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Serialization;

namespace TradingApp.AI.Services;

public sealed class StrategyInterpreter : IStrategyInterpreter
{
    private const int SummaryMaxLength = 100;

    private readonly ILlmClient _llmClient;
    private readonly ILogger<StrategyInterpreter> _logger;

    public StrategyInterpreter(ILlmClient llmClient, ILogger<StrategyInterpreter> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<StrategyIntentDto> InterpretAsync(
        string userText,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userText);

        _logger.LogInformation(
            "Interpreting strategy from natural language input with length {Length}.",
            userText.Length);

        string llmResponse;

        try
        {
            llmResponse = await _llmClient.CompleteAsync(
                StrategyInterpreterPrompt.SystemPrompt,
                userText,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "LLM request failed during strategy interpretation.");
            return CreateFailureResult(
                CreateFailureMessage(ex));
        }

        try
        {
            var payload = NormalizeJsonPayload(llmResponse);
            var intent = JsonSerializer.Deserialize<StrategyIntentDto>(payload, StrategyJsonOptions.Default);

            if (intent is null)
            {
                throw new JsonException("Deserialized strategy intent was null.");
            }

            var config = intent.Config ?? throw new JsonException("Strategy intent config was null.");
            var configWithSource = config with
            {
                Source = new SourceMetadata
                {
                    EntryPoint = StrategyEntryPoint.NaturalLanguage,
                    Summary = $"Generated from natural language: \"{Truncate(userText, SummaryMaxLength)}\"",
                    SourceText = userText,
                },
            };

            var assumptions = intent.Assumptions ?? [];

            return new StrategyIntentDto
            {
                Config = configWithSource,
                Confidence = ClampConfidence(intent.Confidence),
                Assumptions = assumptions,
                ClarificationNeeded = intent.ClarificationNeeded,
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM strategy interpretation response.");
            return CreateFailureResult(
                "Unable to interpret your description. Please try rephrasing or use the form builder.");
        }
    }

    private static decimal ClampConfidence(decimal confidence)
    {
        if (confidence < 0m)
        {
            return 0m;
        }

        if (confidence > 1m)
        {
            return 1m;
        }

        return confidence;
    }

    private static StrategyIntentDto CreateFailureResult(string message)
    {
        return new StrategyIntentDto
        {
            Config = new StrategyConfig(),
            Confidence = 0m,
            Assumptions = [],
            ClarificationNeeded = message,
        };
    }

    private static string CreateFailureMessage(Exception exception)
    {
        if (exception is HttpRequestException httpRequestException)
        {
            if (httpRequestException.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (httpRequestException.Message.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                    httpRequestException.Message.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase))
                {
                    return "The configured LLM API key has no available quota. Check billing and quota limits, or switch to a local provider such as Ollama.";
                }

                return "The LLM provider is rate-limiting requests right now. Please wait a moment and try again.";
            }

            if (httpRequestException.StatusCode == HttpStatusCode.Unauthorized ||
                httpRequestException.StatusCode == HttpStatusCode.Forbidden)
            {
                return "The configured LLM API key was rejected. Verify the key and API access for this project.";
            }

            if (httpRequestException.StatusCode == HttpStatusCode.NotFound)
            {
                if (httpRequestException.Message.Contains("no longer available", StringComparison.OrdinalIgnoreCase) ||
                    httpRequestException.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return "The configured LLM model is not available. Verify the model name in configuration.";
                }
            }
        }

        return "Strategy interpreter is temporarily unavailable. Please try again or use the form builder.";
    }

    private static string NormalizeJsonPayload(string payload)
    {
        var trimmed = payload.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var lines = trimmed.Split('\n');
        var normalizedLines = lines
            .Where(line => !line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            .ToArray();

        return string.Join('\n', normalizedLines).Trim();
    }

    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength
            ? text
            : text[..maxLength] + "...";
    }
}