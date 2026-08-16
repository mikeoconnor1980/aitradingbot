using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.AI.Models;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.AI.Services;

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

        if (AzureOpenAiResponsesProtocol.IsAzureOpenAi(_options.Provider))
        {
            var azureResponse = await AzureOpenAiResponsesProtocol.CompleteAsync(
                _httpClient,
                _options.BaseUrl,
                new AzureResponsesRequest
                {
                    Model = _options.ModelName,
                    Input =
                    [
                        new AzureResponsesInputItem { Role = "system", Content = systemPrompt },
                        new AzureResponsesInputItem { Role = "user", Content = userMessage },
                    ],
                },
                cancellationToken);
            return AzureOpenAiResponsesProtocol.GetText(azureResponse, "Azure OpenAI returned no text output.");
        }

        using var response = await _httpClient.PostAsJsonAsync(
            "chat/completions",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var truncatedBody = errorBody.Length > 500 ? errorBody[..500] + "..." : errorBody;

            _logger.LogWarning(
                "LLM request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                truncatedBody);

            throw new HttpRequestException(
                $"LLM request failed with status code {(int)response.StatusCode}: {truncatedBody}",
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
            _logger.LogWarning(ex, "Failed to deserialize LLM response.");
            throw new InvalidOperationException("LLM returned an unparseable response.", ex);
        }

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