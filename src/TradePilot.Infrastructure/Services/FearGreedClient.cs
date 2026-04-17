using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Infrastructure.Services;

public sealed class FearGreedClient : IFearGreedClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FearGreedClient> _logger;

    public FearGreedClient(HttpClient httpClient, ILogger<FearGreedClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FearGreedReading>> FetchAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var url = $"fng/?limit={limit}";

        _logger.LogDebug("Fetching Fear & Greed Index: GET {Url}", url);

        var response = await _httpClient.GetFromJsonAsync<FearGreedApiResponse>(url, cancellationToken);

        if (response is null)
        {
            _logger.LogWarning("Fear & Greed API returned null response.");
            return [];
        }

        if (!string.IsNullOrEmpty(response.Metadata.Error))
        {
            _logger.LogWarning("Fear & Greed API returned error: {Error}", response.Metadata.Error);
            return [];
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var results = new List<FearGreedReading>(response.Data.Count);

        foreach (var item in response.Data)
        {
            if (!int.TryParse(item.Value, out var value) || !long.TryParse(item.Timestamp, out var timestamp))
            {
                _logger.LogWarning(
                    "Skipping malformed Fear & Greed entry: value={Value}, timestamp={Timestamp}",
                    item.Value,
                    item.Timestamp);
                continue;
            }

            results.Add(FearGreedReading.Create(value, item.ValueClassification, timestamp, now));
        }

        _logger.LogInformation("Fetched {Count} Fear & Greed readings from API.", results.Count);

        return results;
    }
}
