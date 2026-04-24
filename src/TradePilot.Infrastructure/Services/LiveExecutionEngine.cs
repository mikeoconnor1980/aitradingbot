using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;
using TradePilot.Infrastructure.Hyperliquid;
using TradePilot.Infrastructure.Hyperliquid.Models;

namespace TradePilot.Infrastructure.Services;

/// <summary>
/// Self-contained live <see cref="IExecutionEngine"/> that signs and submits orders
/// to Hyperliquid using the locally-held private key. Does not depend on the Api layer.
/// Designed for the Worker service (execution agent running on client machine).
/// </summary>
public sealed class LiveExecutionEngine : IExecutionEngine, IPositionQueryable
{
    private const int FallbackMaxLeverage = 20;
    private const decimal MinimumSpotOrderValueUsd = 10m;

    private readonly IHyperliquidRestClient _restClient;
    private readonly IHyperliquidSigner _signer;
    private readonly INonceProvider _nonceProvider;
    private readonly HyperliquidOptions _options;
    private readonly ILogger<LiveExecutionEngine> _logger;

    private readonly ConcurrentDictionary<string, string> _orderAssetMap = new();
    private readonly ConcurrentDictionary<string, (int Index, int MaxLeverage, int SizeDecimals)> _assetMetadataCache = new();
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
        var (assetIndex, _, sizeDecimals) = await ResolveAssetMetadataAsync(coin, order.AssetType, cancellationToken);
        var isBuy = order.Side == OrderSide.Buy;
        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);
        var isMarket = order.OrderType == OrderType.Market;
        var normalizedSize = NormalizeOrderSize(order.Size, sizeDecimals);

        if (normalizedSize <= 0m)
        {
            _logger.LogWarning(
                "Order size rounded down to zero: Symbol={Symbol}, OriginalSize={OriginalSize}, SizeDecimals={SizeDecimals}, AssetType={AssetType}",
                order.Symbol,
                order.Size,
                sizeDecimals,
                order.AssetType);
            return string.Empty;
        }

        if (normalizedSize != order.Size)
        {
            _logger.LogInformation(
                "Normalized order size for exchange precision: Symbol={Symbol}, OriginalSize={OriginalSize}, NormalizedSize={NormalizedSize}, SizeDecimals={SizeDecimals}, AssetType={AssetType}",
                order.Symbol,
                order.Size,
                normalizedSize,
                sizeDecimals,
                order.AssetType);
        }

        decimal price;
        string tif;
        decimal orderValueUsd;
        decimal rawPrice;
        decimal? referencePrice = null;

        if (isMarket)
        {
            referencePrice = order.AssetType == AssetType.Spot && order.Price > 0m
                ? order.Price
                : await GetMidPriceAsync(coin, cancellationToken);
            rawPrice = isBuy ? referencePrice.Value * 1.05m : referencePrice.Value * 0.95m;
            tif = "Ioc";
            orderValueUsd = normalizedSize * referencePrice.Value;
        }
        else
        {
            rawPrice = order.Price;
            tif = "Gtc";
            orderValueUsd = normalizedSize * rawPrice;
        }

        price = NormalizeOrderPrice(rawPrice, sizeDecimals, order.AssetType);

        if (price <= 0m)
        {
            _logger.LogWarning(
                "Order price rounded down to zero: Symbol={Symbol}, OriginalPrice={OriginalPrice}, SizeDecimals={SizeDecimals}, AssetType={AssetType}",
                order.Symbol,
                rawPrice,
                sizeDecimals,
                order.AssetType);
            return string.Empty;
        }

        if (price != rawPrice)
        {
            _logger.LogInformation(
                "Normalized order price for exchange precision: Symbol={Symbol}, OriginalPrice={OriginalPrice}, NormalizedPrice={NormalizedPrice}, SizeDecimals={SizeDecimals}, AssetType={AssetType}",
                order.Symbol,
                rawPrice,
                price,
                sizeDecimals,
                order.AssetType);
        }

        if (isMarket)
        {
            _logger.LogInformation(
                "Market order: Coin={Coin}, ReferencePrice={ReferencePrice}, SlippagePrice={SlippagePrice}, IsBuy={IsBuy}, AssetType={AssetType}",
                coin, referencePrice, price, isBuy, order.AssetType);
        }

        if (order.AssetType == AssetType.Spot && orderValueUsd < MinimumSpotOrderValueUsd)
        {
            _logger.LogWarning(
                "Spot order below minimum notional: Symbol={Symbol}, NotionalUsd={NotionalUsd}, MinimumUsd={MinimumUsd}, Size={Size}, ReferencePrice={ReferencePrice}",
                order.Symbol,
                orderValueUsd,
                MinimumSpotOrderValueUsd,
                normalizedSize,
                order.Price > 0m ? order.Price : price);
            return string.Empty;
        }

        var action = HyperliquidEip712.BuildOrderAction(
            assetIndex: assetIndex,
            isBuy: isBuy,
            price: price,
            size: normalizedSize,
            reduceOnly: order.ReduceOnly,
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
            order.OrderType, order.Side, order.Symbol, price, normalizedSize, order.TradeType);

        try
        {
            var exchangeResponse = await _restClient
                .PostExchangeAsync<HyperliquidExchangeResponse>(payload, cancellationToken);

            var orderId = ExtractOrderId(exchangeResponse);

            if (string.IsNullOrEmpty(orderId))
            {
                _logger.LogWarning(
                    "Order rejected by exchange: Symbol={Symbol}, Status={Status}, Detail={Detail}",
                    order.Symbol,
                    exchangeResponse.Status,
                    GetExchangeErrorDetail(exchangeResponse));
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

        await CancelOrderAsync(orderId, asset, cancellationToken);
    }

    public async Task CancelOrderAsync(string orderId, string asset, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(asset);

        var coin = HyperliquidAssetMapper.ToCoin(asset);
        var (assetIndex, _, _) = await ResolveAssetMetadataAsync(coin, AssetType.Perp, cancellationToken);
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
        var (assetIndex, _, _) = await ResolveAssetMetadataAsync(coin, AssetType.Perp, cancellationToken);
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

    public async Task<string> PlaceTriggerOrderAsync(string asset, string side, decimal size, decimal triggerPrice, string tpslType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(asset);

        var coin = HyperliquidAssetMapper.ToCoin(asset);
        var (assetIndex, _, _) = await ResolveAssetMetadataAsync(coin, AssetType.Perp, cancellationToken);
        var isBuy = side.Equals("buy", StringComparison.OrdinalIgnoreCase);
        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);

        var action = HyperliquidEip712.BuildTriggerOrderAction(assetIndex, isBuy, triggerPrice, size, tpslType);

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
            "Placing trigger order: Asset={Asset}, Side={Side}, TriggerPrice={TriggerPrice}, Size={Size}, TpslType={TpslType}",
            asset, side, triggerPrice, size, tpslType);

        var exchangeResponse = await _restClient
            .PostExchangeAsync<HyperliquidExchangeResponse>(payload, cancellationToken);

        var orderId = ExtractOrderId(exchangeResponse);

        if (string.IsNullOrEmpty(orderId))
        {
            _logger.LogWarning("Trigger order rejected by exchange: Asset={Asset}", asset);
            return string.Empty;
        }

        _logger.LogInformation("Trigger order placed: OrderId={OrderId}, Asset={Asset}", orderId, asset);
        return orderId;
    }

    public async Task ModifyTriggerOrderAsync(string orderId, string asset, string side, decimal triggerPrice, decimal size, string tpslType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        var coin = HyperliquidAssetMapper.ToCoin(asset);
        var (assetIndex, _, _) = await ResolveAssetMetadataAsync(coin, AssetType.Perp, cancellationToken);
        var isBuy = side.Equals("buy", StringComparison.OrdinalIgnoreCase);
        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);

        var action = new HyperliquidModifyAction
        {
            Modifies =
            [
                new HyperliquidModifyEntry
                {
                    OrderId = long.Parse(orderId),
                    Order = new HyperliquidModifyOrderParams
                    {
                        AssetIndex = assetIndex,
                        IsBuy = isBuy,
                        Price = HyperliquidFormatting.ToWireDecimal(triggerPrice),
                        Size = HyperliquidFormatting.ToWireDecimal(size),
                        ReduceOnly = true,
                        OrderType = new HyperliquidOrderType
                        {
                            Trigger = new HyperliquidTriggerParams
                            {
                                TriggerPx = HyperliquidFormatting.ToWireDecimal(triggerPrice),
                                IsMarket = true,
                                Tpsl = tpslType,
                            },
                        },
                    },
                },
            ],
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

        _logger.LogInformation(
            "Modifying trigger order: OrderId={OrderId}, Asset={Asset}, TriggerPrice={TriggerPrice}, Size={Size}, TpslType={TpslType}",
            orderId, asset, triggerPrice, size, tpslType);

        await _restClient.PostExchangeAsync<HyperliquidExchangeResponse>(payload, cancellationToken);
    }

    public async Task SetLeverageAsync(string asset, int leverage, bool isIsolated, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(asset);

        var coin = HyperliquidAssetMapper.ToCoin(asset);
        var (assetIndex, maxLeverage, _) = await ResolveAssetMetadataAsync(coin, AssetType.Perp, cancellationToken);
        var requestedLeverage = leverage;
        var clampedLeverage = Math.Clamp(requestedLeverage, 1, maxLeverage);

        if (requestedLeverage > maxLeverage)
        {
            _logger.LogWarning(
                "Leverage {Requested}x exceeds max {Max}x for {Asset}. Clamping to {Max}x.",
                requestedLeverage,
                maxLeverage,
                asset,
                maxLeverage);
        }

        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);
        var action = new Dictionary<string, object>
        {
            ["type"] = "updateLeverage",
            ["asset"] = assetIndex,
            ["isCross"] = !isIsolated,
            ["leverage"] = clampedLeverage,
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

        await _restClient.PostExchangeAsync<JsonElement>(payload, cancellationToken);

        _logger.LogInformation(
            "Set leverage for {Asset}: {Leverage}x, isolated={IsIsolated}",
            coin,
            clampedLeverage,
            isIsolated);
    }

    private async Task<(int Index, int MaxLeverage, int SizeDecimals)> ResolveAssetMetadataAsync(
        string asset,
        AssetType assetType,
        CancellationToken cancellationToken)
    {
        var coin = NormalizeCoin(asset);
        var cacheKey = BuildAssetCacheKey(coin, assetType);
        if (_assetMetadataCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        await _metadataLock.WaitAsync(cancellationToken);
        try
        {
            if (_assetMetadataCache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            if (assetType == AssetType.Spot)
            {
                cached = await LoadSpotAssetMetadataAsync(coin, cancellationToken);
                _assetMetadataCache[cacheKey] = cached;
                return cached;
            }

            var response = await _restClient.PostInfoAsync<JsonElement>(
                new { type = "meta" }, cancellationToken);

            if (response.TryGetProperty("universe", out var universe))
            {
                for (var i = 0; i < universe.GetArrayLength(); i++)
                {
                    var item = universe[i];
                    var name = item.GetProperty("name").GetString();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var maxLeverage = item.TryGetProperty("maxLeverage", out var maxLeverageElement)
                        && maxLeverageElement.TryGetInt32(out var parsedMaxLeverage)
                        && parsedMaxLeverage > 0
                        ? parsedMaxLeverage
                        : FallbackMaxLeverage;
                    var sizeDecimals = item.TryGetProperty("szDecimals", out var sizeDecimalsElement)
                        && sizeDecimalsElement.TryGetInt32(out var parsedSizeDecimals)
                        && parsedSizeDecimals >= 0
                        ? parsedSizeDecimals
                        : 5;

                    _assetMetadataCache[BuildAssetCacheKey(name, AssetType.Perp)] = (i, maxLeverage, sizeDecimals);
                }
            }

            if (_assetMetadataCache.TryGetValue(cacheKey, out cached))
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

    private async Task<(int Index, int MaxLeverage, int SizeDecimals)> LoadSpotAssetMetadataAsync(string coin, CancellationToken cancellationToken)
    {
        var response = await _restClient.PostInfoAsync<JsonElement>(
            new { type = "spotMeta" }, cancellationToken);

        if (!response.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Hyperliquid spot metadata did not include tokens.");
        }

        if (!response.TryGetProperty("universe", out var universe) || universe.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Hyperliquid spot metadata did not include universe.");
        }

        var quoteTokenIndex = GetSpotTokenIndex(tokens, "USDC");
        var baseTokenIndex = GetSpotTokenIndex(tokens, coin);
        var baseTokenDecimals = GetSpotTokenSizeDecimals(tokens, coin);

        foreach (var pair in universe.EnumerateArray())
        {
            if (!pair.TryGetProperty("tokens", out var pairTokens) || pairTokens.GetArrayLength() < 2)
            {
                continue;
            }

            if (pairTokens[0].GetInt32() != baseTokenIndex || pairTokens[1].GetInt32() != quoteTokenIndex)
            {
                continue;
            }

            var pairIndex = pair.TryGetProperty("index", out var pairIndexElement)
                ? pairIndexElement.GetInt32()
                : throw new InvalidOperationException($"Hyperliquid spot pair '{coin}/USDC' did not include an index.");

            return (10_000 + pairIndex, 1, baseTokenDecimals);
        }

        throw new InvalidOperationException($"Spot pair '{coin}/USDC' not found in Hyperliquid spot metadata.");
    }

    private static int GetSpotTokenIndex(JsonElement tokens, string tokenName)
    {
        foreach (var token in tokens.EnumerateArray())
        {
            if (!token.TryGetProperty("name", out var nameElement)
                || !string.Equals(nameElement.GetString(), tokenName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!token.TryGetProperty("index", out var indexElement))
            {
                break;
            }

            return indexElement.GetInt32();
        }

        throw new InvalidOperationException($"Spot token '{tokenName}' not found in Hyperliquid spot metadata.");
    }

    private static int GetSpotTokenSizeDecimals(JsonElement tokens, string tokenName)
    {
        foreach (var token in tokens.EnumerateArray())
        {
            if (!token.TryGetProperty("name", out var nameElement)
                || !string.Equals(nameElement.GetString(), tokenName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (token.TryGetProperty("szDecimals", out var sizeDecimalsElement))
            {
                return sizeDecimalsElement.GetInt32();
            }

            break;
        }

        throw new InvalidOperationException($"Spot token '{tokenName}' did not include szDecimals in Hyperliquid spot metadata.");
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

    private static string GetExchangeErrorDetail(HyperliquidExchangeResponse response)
    {
        if (response.Response?.Data?.Statuses is { Count: > 0 } statuses)
        {
            var error = statuses[0].Error;
            if (!string.IsNullOrWhiteSpace(error))
            {
                return error;
            }
        }

        if (!string.IsNullOrWhiteSpace(response.Response?.ErrorMessage))
        {
            return response.Response.ErrorMessage;
        }

        return "No exchange detail returned.";
    }

    private static string NormalizeCoin(string asset)
    {
        return asset.EndsWith("-PERP", StringComparison.OrdinalIgnoreCase)
            ? HyperliquidAssetMapper.ToCoin(asset)
            : asset;
    }

    private static string BuildAssetCacheKey(string coin, AssetType assetType)
    {
        return $"{assetType}:{coin}";
    }

    private static decimal RoundToSignificantFigures(decimal value, int significantFigures)
    {
        if (value == 0m) return 0m;
        var magnitude = (int)Math.Floor(Math.Log10((double)Math.Abs(value)));
        var factor = (decimal)Math.Pow(10, significantFigures - 1 - magnitude);
        return Math.Round(value * factor) / factor;
    }

    private static decimal NormalizeOrderSize(decimal size, int sizeDecimals)
    {
        if (sizeDecimals < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeDecimals));
        }

        if (size == 0m)
        {
            return 0m;
        }

        var sign = Math.Sign(size);
        var absoluteSize = Math.Abs(size);
        var factor = (decimal)Math.Pow(10, sizeDecimals);
        var normalized = decimal.Truncate(absoluteSize * factor) / factor;
        return normalized * sign;
    }

    private static decimal NormalizeOrderPrice(decimal price, int sizeDecimals, AssetType assetType)
    {
        if (price == 0m)
        {
            return 0m;
        }

        var roundedToSigFigs = RoundToSignificantFigures(price, 5);
        var maxDecimals = assetType == AssetType.Spot
            ? Math.Max(0, 8 - sizeDecimals)
            : Math.Max(0, 6 - sizeDecimals);
        var factor = (decimal)Math.Pow(10, maxDecimals);

        return decimal.Truncate(roundedToSigFigs * factor) / factor;
    }

    public async Task<PositionState> QueryPositionAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var response = await _restClient.PostInfoAsync<JsonElement>(
            new { type = "clearinghouseState", user = _signer.WalletAddress }, cancellationToken);

        var coin = HyperliquidAssetMapper.ToCoin(symbol);

        if (response.TryGetProperty("assetPositions", out var positions))
        {
            foreach (var pos in positions.EnumerateArray())
            {
                if (!pos.TryGetProperty("position", out var position))
                {
                    continue;
                }

                var positionCoin = position.TryGetProperty("coin", out var coinProp)
                    ? coinProp.GetString() : null;

                if (!string.Equals(positionCoin, coin, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var szi = position.TryGetProperty("szi", out var sziProp) && decimal.TryParse(sziProp.GetString(), out var s) ? s : 0m;
                var entryPx = position.TryGetProperty("entryPx", out var entryProp) && decimal.TryParse(entryProp.GetString(), out var e) ? e : 0m;
                var unrealizedPnl = position.TryGetProperty("unrealizedPnl", out var pnlProp) && decimal.TryParse(pnlProp.GetString(), out var p) ? p : 0m;

                return new PositionState
                {
                    Symbol = symbol,
                    Size = szi,
                    AverageEntryPrice = entryPx,
                    UnrealisedPnL = unrealizedPnl,
                };
            }
        }

        return new PositionState { Symbol = symbol };
    }

    public async Task<decimal> QueryAccountEquityAsync(CancellationToken cancellationToken = default)
    {
        var response = await _restClient.PostInfoAsync<JsonElement>(
            new { type = "clearinghouseState", user = _signer.WalletAddress }, cancellationToken);

        if (response.TryGetProperty("marginSummary", out var margin)
            && margin.TryGetProperty("accountValue", out var accountValue)
            && decimal.TryParse(accountValue.GetString(), out var equity))
        {
            return equity;
        }

        return 0m;
    }
}
