using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.AI.Models;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.AI.Services;

/// <summary>
/// LLM client for market context analysis, independently configured via LlmContextOptions.
/// Reuses the same OpenAI-compatible HTTP protocol as <see cref="OpenAiCompatibleLlmClient"/>.
/// </summary>
public sealed class LlmContextClient : ILlmContextClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LlmContextClient> _logger;
    private readonly LlmContextOptions _options;

    public LlmContextClient(
        HttpClient httpClient,
        IOptions<LlmContextOptions> options,
        ILogger<LlmContextClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        var request = new ChatCompletionRequest
        {
            Model = _options.ModelName,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userMessage },
            ],
            Temperature = 0.2m,
            ResponseFormat = new ResponseFormat { Type = "json_object" },
        };

        _logger.LogInformation(
            "Sending market context LLM request to {Provider} model {Model}",
            _options.Provider,
            _options.ModelName);

        using var response = await _httpClient.PostAsJsonAsync(
            "chat/completions",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var truncatedBody = errorBody.Length > 500 ? errorBody[..500] + "..." : errorBody;

            _logger.LogWarning(
                "Context LLM request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                truncatedBody);

            throw new HttpRequestException(
                $"Context LLM request failed with status code {(int)response.StatusCode}: {truncatedBody}",
                null,
                response.StatusCode);
        }

        ChatCompletionResponse? result;

        try
        {
            result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
                cancellationToken: cancellationToken);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize context LLM response.");
            throw new InvalidOperationException("Context LLM returned an unparseable response.", ex);
        }

        if (result?.Choices is not { Count: > 0 })
        {
            throw new InvalidOperationException("Context LLM returned no choices.");
        }

        var content = result.Choices[0].Message.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Context LLM returned an empty completion.");
        }

        return content;
    }
}
