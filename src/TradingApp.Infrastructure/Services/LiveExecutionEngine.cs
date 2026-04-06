using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Enums;
using TradingApp.Infrastructure.Hyperliquid;
using TradingApp.Infrastructure.Hyperliquid.Models;

namespace TradingApp.Infrastructure.Services;

/// <summary>
/// Self-contained live <see cref="IExecutionEngine"/> that signs and submits orders
/// to Hyperliquid using the locally-held private key. Does not depend on the Api layer.
/// Designed for the Worker service (execution agent running on client machine).
/// </summary>
public sealed class LiveExecutionEngine : IExecutionEngine
{
    private readonly IHyperliquidRestClient _restClient;
    private readonly IHyperliquidSigner _signer;
    private readonly INonceProvider _nonceProvider;
    private readonly HyperliquidOptions _options;
    private readonly ILogger<LiveExecutionEngine> _logger;

    private readonly ConcurrentDictionary<string, string> _orderAssetMap = new();
    private readonly ConcurrentDictionary<string, int> _assetIndexCache = new();
    private readonly SemaphoreSlim _metadataLock = new(1, 1);

    public LiveExecutionEngine(
        IHyperliquidRestClient restClient,
        IHyperliquidSigner signer,
        INonceProvider nonceProvider,
        IOptions<HyperliquidOptions> options,
        ILogger<LiveExecutionEngine> logger)
    {
        _restClient = restClient;
        _signer = signer;
        _nonceProvider = nonceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> PlaceOrderAsync(OrderRequest order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var coin = HyperliquidAssetMapper.ToCoin(order.Symbol);
        var assetIndex = await ResolveAssetIndexAsync(coin, cancellationToken);
        var isBuy = order.Side == OrderSide.Buy;
        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);
        var isMarket = order.OrderType == OrderType.Market;

        decimal price;
        string tif;

        if (isMarket)
        {
            var midPrice = await GetMidPriceAsync(coin, cancellationToken);
            price = RoundToSignificantFigures(isBuy ? midPrice * 1.05m : midPrice * 0.95m, 5);
            tif = "Ioc";

            _logger.LogInformation(
                "Market order: Coin={Coin}, MidPrice={MidPrice}, SlippagePrice={SlippagePrice}, IsBuy={IsBuy}",
                coin, midPrice, price, isBuy);
        }
        else
        {
            price = order.Price;
            tif = "Gtc";
        }

        var action = HyperliquidEip712.BuildOrderAction(
            assetIndex: assetIndex,
            isBuy: isBuy,
            price: price,
            size: order.Size,
            tif: tif);

        var nonce = _nonceProvider.GetNextNonce();
        var connectionId = HyperliquidEip712.ComputeActionHash(action, nonce, vaultAddress: null);
        var eip712Hash = HyperliquidEip712.ComputeEip712Hash(connectionId, isMainnet);
        var (r, s, v) = _signer.SignHash(eip712Hash);

        var payload = new
        {
            action,
            nonce,
            signature = new { r, s, v },
            vaultAddress = (string?)null,
        };

        _logger.LogInformation(
            "Placing {OrderType} {Side} order: Symbol={Symbol}, Price={Price}, Size={Size}, TradeType={TradeType}",
            order.OrderType, order.Side, order.Symbol, price, order.Size, order.TradeType);

        try
        {
            var exchangeResponse = await _restClient
                .PostExchangeAsync<HyperliquidExchangeResponse>(payload, cancellationToken);

            var orderId = ExtractOrderId(exchangeResponse);

            if (string.IsNullOrEmpty(orderId))
            {
                _logger.LogWarning("Order rejected by exchange: Symbol={Symbol}", order.Symbol);
                return string.Empty;
            }

            _orderAssetMap[orderId] = order.Symbol;

            _logger.LogInformation(
                "Order placed: OrderId={OrderId}, Symbol={Symbol}", orderId, order.Symbol);

            return orderId;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Order submission failed (network error): Symbol={Symbol}", order.Symbol);
            return string.Empty;
        }
    }

    public async Task CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        if (!_orderAssetMap.TryGetValue(orderId, out var asset))
        {
            _logger.LogWarning(
                "Cannot cancel order {OrderId}: asset mapping not found.", orderId);
            return;
        }

        var coin = HyperliquidAssetMapper.ToCoin(asset);
        var assetIndex = await ResolveAssetIndexAsync(coin, cancellationToken);
        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);

        var action = new Dictionary<string, object>
        {
            ["type"] = "cancel",
            ["cancels"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["a"] = assetIndex,
                    ["o"] = long.Parse(orderId)
                }
            }
        };

        var nonce = _nonceProvider.GetNextNonce();
        var connectionId = HyperliquidEip712.ComputeActionHash(action, nonce, vaultAddress: null);
        var eip712Hash = HyperliquidEip712.ComputeEip712Hash(connectionId, isMainnet);
        var (r, s, v) = _signer.SignHash(eip712Hash);

        var payload = new
        {
            action,
            nonce,
            signature = new { r, s, v },
            vaultAddress = (string?)null,
        };

        _logger.LogInformation("Cancelling order: OrderId={OrderId}, Asset={Asset}", orderId, asset);

        await _restClient.PostExchangeAsync<HyperliquidExchangeResponse>(payload, cancellationToken);
        _orderAssetMap.TryRemove(orderId, out _);
    }

    public async Task CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var coin = HyperliquidAssetMapper.ToCoin(symbol);
        var assetIndex = await ResolveAssetIndexAsync(coin, cancellationToken);
        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);

        var action = new Dictionary<string, object>
        {
            ["type"] = "cancelByCloid",
            ["cancels"] = new[]
            {
                new Dictionary<string, object> { ["asset"] = assetIndex }
            }
        };

        var nonce = _nonceProvider.GetNextNonce();
        var connectionId = HyperliquidEip712.ComputeActionHash(action, nonce, vaultAddress: null);
        var eip712Hash = HyperliquidEip712.ComputeEip712Hash(connectionId, isMainnet);
        var (r, s, v) = _signer.SignHash(eip712Hash);

        var payload = new
        {
            action,
            nonce,
            signature = new { r, s, v },
            vaultAddress = (string?)null,
        };

        _logger.LogInformation("Cancelling all orders for: Symbol={Symbol}", symbol);

        await _restClient.PostExchangeAsync<HyperliquidExchangeResponse>(payload, cancellationToken);

        var keysToRemove = _orderAssetMap
            .Where(kvp => kvp.Value.Equals(symbol, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _orderAssetMap.TryRemove(key, out _);
        }
    }

    private async Task<int> ResolveAssetIndexAsync(string coin, CancellationToken cancellationToken)
    {
        if (_assetIndexCache.TryGetValue(coin, out var cached))
        {
            return cached;
        }

        await _metadataLock.WaitAsync(cancellationToken);
        try
        {
            if (_assetIndexCache.TryGetValue(coin, out cached))
            {
                return cached;
            }

            var response = await _restClient.PostInfoAsync<JsonElement>(
                new { type = "meta" }, cancellationToken);

            if (response.TryGetProperty("universe", out var universe))
            {
                for (var i = 0; i < universe.GetArrayLength(); i++)
                {
                    var name = universe[i].GetProperty("name").GetString();
                    if (name is not null)
                    {
                        _assetIndexCache[name] = i;
                    }
                }
            }

            if (_assetIndexCache.TryGetValue(coin, out cached))
            {
                return cached;
            }

            throw new InvalidOperationException($"Asset '{coin}' not found in Hyperliquid universe metadata.");
        }
        finally
        {
            _metadataLock.Release();
        }
    }

    private async Task<decimal> GetMidPriceAsync(string coin, CancellationToken cancellationToken)
    {
        var displayName = HyperliquidAssetMapper.ToDisplayName(coin);
        var marketInfo = await _restClient.GetMarketInfoAsync(displayName, cancellationToken);

        if (marketInfo is null || marketInfo.MidPrice <= 0)
        {
            throw new InvalidOperationException($"Could not resolve mid price for '{coin}'.");
        }

        return marketInfo.MidPrice;
    }

    private static string? ExtractOrderId(HyperliquidExchangeResponse response)
    {
        if (!string.Equals(response.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var statuses = response.Response?.Data?.Statuses;
        if (statuses is null || statuses.Count == 0)
        {
            return null;
        }

        var first = statuses[0];
        return first.Resting?.Oid.ToString() ?? first.Filled?.Oid.ToString();
    }

    private static decimal RoundToSignificantFigures(decimal value, int significantFigures)
    {
        if (value == 0m) return 0m;
        var magnitude = (int)Math.Floor(Math.Log10((double)Math.Abs(value)));
        var factor = (decimal)Math.Pow(10, significantFigures - 1 - magnitude);
        return Math.Round(value * factor) / factor;
    }
}
