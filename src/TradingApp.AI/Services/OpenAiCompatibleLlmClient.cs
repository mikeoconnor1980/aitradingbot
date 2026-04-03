using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.AI.Models;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.AI.Services;

public sealed class OpenAiCompatibleLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiCompatibleLlmClient> _logger;
    private readonly LlmOptions _options;

    public OpenAiCompatibleLlmClient(
        HttpClient httpClient,
        IOptions<LlmOptions> options,
        ILogger<OpenAiCompatibleLlmClient> logger)
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
        };

        _logger.LogInformation(
            "Sending LLM request to {Provider} model {Model}",
            _options.Provider,
            _options.ModelName);

        using var response = await _httpClient.PostAsJsonAsync(
            "chat/completions",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogWarning(
                "LLM request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                errorBody);

            throw new HttpRequestException(
                $"LLM request failed with status code {(int)response.StatusCode}: {errorBody}",
                null,
                response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
            cancellationToken: cancellationToken);

        if (result?.Choices is not { Count: > 0 })
        {
            throw new InvalidOperationException("LLM returned no choices.");
        }

        var content = result.Choices[0].Message.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("LLM returned an empty completion.");
        }

        return content;
    }
}