using System.Net.Http.Json;
using TradePilot.AI.Models;

namespace TradePilot.AI.Services;

internal static class AzureOpenAiResponsesProtocol
{
    public static bool IsAzureOpenAi(string provider) =>
        string.Equals(provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase);

    public static async Task<AzureResponsesResponse> CompleteAsync(
        HttpClient httpClient,
        string endpoint,
        AzureResponsesRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var truncatedBody = errorBody.Length > 500 ? errorBody[..500] + "..." : errorBody;
            throw new HttpRequestException(
                $"Azure OpenAI Responses request failed with status code {(int)response.StatusCode}: {truncatedBody}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<AzureResponsesResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Azure OpenAI returned an empty response.");
    }

    public static string GetText(AzureResponsesResponse response, string emptyResponseMessage)
    {
        var text = string.Concat(response.Output
            .Where(item => string.Equals(item.Type, "message", StringComparison.OrdinalIgnoreCase))
            .SelectMany(item => item.Content ?? [])
            .Where(item => string.Equals(item.Type, "output_text", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Text));

        return string.IsNullOrWhiteSpace(text)
            ? throw new InvalidOperationException(emptyResponseMessage)
            : text;
    }
}