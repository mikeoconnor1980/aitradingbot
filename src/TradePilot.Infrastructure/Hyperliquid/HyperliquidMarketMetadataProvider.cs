using System.Text.Json;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Hyperliquid;

public sealed class HyperliquidMarketMetadataProvider : IExchangeMarketMetadataProvider
{
    private readonly IHyperliquidRestClient _restClient;

    public HyperliquidMarketMetadataProvider(IHyperliquidRestClient restClient)
    {
        _restClient = restClient;
    }

    public Exchange Exchange => Exchange.Hyperliquid;

    public Task<MarketInfoDto?> GetMarketInfoAsync(TradingPair pair, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pair);
        return _restClient.GetMarketInfoAsync(pair.Base, cancellationToken);
    }

    public async Task<int?> GetMaxLeverageAsync(TradingPair pair, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pair);

        var response = await _restClient.PostInfoAsync<JsonElement>(new { type = "meta" }, cancellationToken);
        if (!response.TryGetProperty("universe", out var universe) || universe.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in universe.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out var nameProp) ||
                !string.Equals(nameProp.GetString(), pair.Base, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (asset.TryGetProperty("maxLeverage", out var maxLeverageProp))
            {
                if (maxLeverageProp.ValueKind == JsonValueKind.Number)
                {
                    return maxLeverageProp.GetInt32();
                }

                if (maxLeverageProp.ValueKind == JsonValueKind.String &&
                    int.TryParse(maxLeverageProp.GetString(), out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        return null;
    }
}