using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.AI.Models;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.AI.Services;

/// <summary>
/// LLM client for strategy reviews, independently configured via LlmReviewOptions.
/// Reuses the same OpenAI-compatible HTTP protocol as <see cref="OpenAiCompatibleLlmClient"/>.
/// </summary>
public sealed class ReviewLlmClient : IReviewLlmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReviewLlmClient> _logger;
    private readonly LlmReviewOptions _options;

    public ReviewLlmClient(
        HttpClient httpClient,
        IOptions<LlmReviewOptions> options,
        ILogger<ReviewLlmClient> logger)
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
            Temperature = 0.4m,
            ResponseFormat = new ResponseFormat { Type = "text" },
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "chat/completions",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var truncatedBody = errorBody.Length > 500 ? errorBody[..500] + "..." : errorBody;

            _logger.LogWarning(
                "Review LLM request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                truncatedBody);

            throw new HttpRequestException(
                $"Review LLM request failed with status code {(int)response.StatusCode}: {truncatedBody}",
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
            _logger.LogWarning(ex, "Failed to deserialize review LLM response.");
            throw new InvalidOperationException("Review LLM returned an unparseable response.", ex);
        }

        if (result?.Choices is not { Count: > 0 })
        {
            throw new InvalidOperationException("Review LLM returned no choices.");
        }

        var content = result.Choices[0].Message.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Review LLM returned an empty completion.");
        }

        return content;
    }
}