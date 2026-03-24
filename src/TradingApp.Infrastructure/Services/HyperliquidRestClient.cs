using System.Net.Http.Json;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.Infrastructure.Services;

public sealed class HyperliquidRestClient : IHyperliquidRestClient
{
    private readonly HttpClient _httpClient;

    public HyperliquidRestClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/info",
            new { type = "meta" },
            cancellationToken);

        return response.IsSuccessStatusCode;
    }
}
