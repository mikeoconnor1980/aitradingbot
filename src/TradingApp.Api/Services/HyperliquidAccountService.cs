using System.Globalization;
using System.Text.Json;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.Api.Services;

public sealed class HyperliquidAccountService : IHyperliquidAccountService
{
    private readonly IHyperliquidRestClient _restClient;
    private readonly IHyperliquidSigner _signer;
    private readonly ILogger<HyperliquidAccountService> _logger;

    public HyperliquidAccountService(
        IHyperliquidRestClient restClient,
        IHyperliquidSigner signer,
        ILogger<HyperliquidAccountService> logger)
    {
        _restClient = restClient;
        _signer = signer;
        _logger = logger;
    }

    public async Task<AccountSummaryDto> GetAccountSummaryAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetClearinghouseStateAsync(cancellationToken);
        return MapToAccountSummary(response);
    }

    public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        var stateTask = GetClearinghouseStateAsync(cancellationToken);
        var contextsTask = GetAssetContextsAsync(cancellationToken);

        await Task.WhenAll(stateTask, contextsTask);

        return MapToPositions(await stateTask, await contextsTask);
    }

    public async Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(CancellationToken cancellationToken = default)
    {
        var request = new { type = "openOrders", user = _signer.WalletAddress };
        var response = await _restClient.PostInfoAsync<JsonElement>(request, cancellationToken);
        return MapToOpenOrders(response);
    }

    private async Task<JsonElement> GetClearinghouseStateAsync(CancellationToken cancellationToken)
    {
        var request = new { type = "clearinghouseState", user = _signer.WalletAddress };
        var response = await _restClient.PostInfoAsync<JsonElement>(request, cancellationToken);

        if (response.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("Unexpected clearinghouseState response shape: {Kind}", response.ValueKind);
        }

        return response;
    }

    private async Task<AssetContextLookup> GetAssetContextsAsync(CancellationToken cancellationToken)
    {
        var result = new AssetContextLookup();

        try
        {
            var request = new { type = "metaAndAssetCtxs" };
            var response = await _restClient.PostInfoAsync<JsonElement>(request, cancellationToken);

            if (response.ValueKind != JsonValueKind.Array || response.GetArrayLength() < 2)
            {
                return result;
            }

            var meta = response[0];
            var assetCtxs = response[1];

            if (!TryGetProperty(meta, "universe", out var universe) ||
                universe.ValueKind != JsonValueKind.Array ||
                assetCtxs.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            var universeArray = universe.EnumerateArray().ToArray();
            var ctxsArray = assetCtxs.EnumerateArray().ToArray();

            for (var i = 0; i < Math.Min(universeArray.Length, ctxsArray.Length); i++)
            {
                var coin = GetString(GetPropertyOrDefault(universeArray[i], "name"));

                if (!string.IsNullOrEmpty(coin))
                {
                    result.MarkPrices[coin] = ParseDecimal(GetPropertyOrDefault(ctxsArray[i], "markPx"));
                    result.FundingRates[coin] = ParseDecimal(GetPropertyOrDefault(ctxsArray[i], "funding"));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch asset contexts; positions will have zero mark price and funding rate");
        }

        return result;
    }

    private sealed class AssetContextLookup
    {
        public Dictionary<string, decimal> MarkPrices { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, decimal> FundingRates { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static AccountSummaryDto MapToAccountSummary(JsonElement response)
    {
        var equity = ParseDecimal(GetPropertyOrDefault(
            GetPropertyOrDefault(response, "marginSummary"),
            "accountValue"));

        var availableMargin = ParseDecimal(GetPropertyOrDefault(response, "withdrawable"));
        var maintenanceMargin = ParseDecimal(GetPropertyOrDefault(response, "crossMaintenanceMarginUsed"));

        var unrealisedPnl = 0m;
        if (TryGetProperty(response, "assetPositions", out var assetPositions) &&
            assetPositions.ValueKind == JsonValueKind.Array)
        {
            foreach (var assetPosition in assetPositions.EnumerateArray())
            {
                var position = UnwrapPosition(assetPosition);
                unrealisedPnl += ParseDecimal(GetPropertyOrDefault(position, "unrealizedPnl"));
            }
        }

        var crossMarginRatio = equity > 0m
            ? maintenanceMargin / equity
            : 0m;

        return new AccountSummaryDto
        {
            Equity = equity,
            AvailableMargin = availableMargin,
            CrossMarginRatio = crossMarginRatio,
            MaintenanceMargin = maintenanceMargin,
            UnrealisedPnl = unrealisedPnl,
        };
    }

    private static IReadOnlyList<PositionDto> MapToPositions(JsonElement response, AssetContextLookup contexts)
    {
        var results = new List<PositionDto>();

        if (!TryGetProperty(response, "assetPositions", out var assetPositions) ||
            assetPositions.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var assetPosition in assetPositions.EnumerateArray())
        {
            var position = UnwrapPosition(assetPosition);

            var size = ParseDecimal(GetPropertyOrDefault(position, "szi"));
            var entryPrice = ParseDecimal(GetPropertyOrDefault(position, "entryPx"));
            var pnlPercent = ParseDecimal(GetPropertyOrDefault(position, "returnOnEquity"));
            var marginUsed = ParseDecimal(GetPropertyOrDefault(position, "marginUsed"));

            pnlPercent *= 100m;

            var (leverage, marginMode) = ExtractLeverage(position);
            var coin = GetString(GetPropertyOrDefault(position, "coin"));

            contexts.MarkPrices.TryGetValue(coin, out var markPrice);
            contexts.FundingRates.TryGetValue(coin, out var fundingRate);

            results.Add(new PositionDto
            {
                Asset = coin,
                Size = size,
                Side = size >= 0m ? "Long" : "Short",
                EntryPrice = entryPrice,
                MarkPrice = markPrice,
                UnrealisedPnl = ParseDecimal(GetPropertyOrDefault(position, "unrealizedPnl")),
                UnrealisedPnlPercent = pnlPercent,
                LiquidationPrice = ParseDecimal(GetPropertyOrDefault(position, "liquidationPx")),
                Leverage = leverage,
                MarginMode = marginMode,
                MarginUsed = marginUsed,
                FundingRate = fundingRate,
            });
        }

        return results;
    }

    private static IReadOnlyList<OpenOrderDto> MapToOpenOrders(JsonElement response)
    {
        var results = new List<OpenOrderDto>();

        if (response.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var order in response.EnumerateArray())
        {
            if (order.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var size = ParseDecimal(GetPropertyOrDefault(order, "sz"));

            results.Add(new OpenOrderDto
            {
                OrderId = GetString(GetPropertyOrDefault(order, "oid")),
                Asset = GetString(GetPropertyOrDefault(order, "coin")),
                Side = MapOrderSide(GetString(GetPropertyOrDefault(order, "side"))),
                Price = ParseDecimal(GetPropertyOrDefault(order, "limitPx")),
                Size = size,
                OrderType = GetOrderType(order),
                Status = GetString(GetPropertyOrDefault(order, "status")),
            });
        }

        return results;
    }

    private static (int Leverage, string MarginMode) ExtractLeverage(JsonElement assetPosition)
    {
        if (TryGetProperty(assetPosition, "leverage", out var leverageObj) &&
            leverageObj.ValueKind == JsonValueKind.Object)
        {
            var value = (int)ParseDecimal(GetPropertyOrDefault(leverageObj, "value"));
            var type = GetString(GetPropertyOrDefault(leverageObj, "type"));
            return (value, type);
        }

        return (0, string.Empty);
    }

    private static JsonElement UnwrapPosition(JsonElement assetPosition)
    {
        if (TryGetProperty(assetPosition, "position", out var position))
        {
            return position;
        }

        return assetPosition;
    }

    private static string GetOrderType(JsonElement order)
    {
        if (!TryGetProperty(order, "orderType", out var orderType))
        {
            return string.Empty;
        }

        if (orderType.ValueKind == JsonValueKind.String)
        {
            return orderType.GetString() ?? string.Empty;
        }

        if (orderType.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in orderType.EnumerateObject())
            {
                return property.Name;
            }
        }

        return string.Empty;
    }

    private static string MapOrderSide(string side)
    {
        return side.ToUpperInvariant() switch
        {
            "B" => "Buy",
            "A" => "Sell",
            _ => side,
        };
    }

    private static JsonElement GetPropertyOrDefault(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var value)
            ? value
            : default;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static decimal ParseDecimal(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetDecimal();
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString();
            if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return 0m;
    }

    private static string GetString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            _ => string.Empty,
        };
    }
}