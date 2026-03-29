using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Infrastructure.Hyperliquid;
using TradingApp.Infrastructure.Hyperliquid.Models;

namespace TradingApp.Api.Services;

public sealed class HyperliquidOrderService : IHyperliquidOrderService
{
    private readonly IHyperliquidRestClient _restClient;
    private readonly IHyperliquidSigner _signer;
    private readonly INonceProvider _nonceProvider;
    private readonly IHyperliquidAccountService _accountService;
    private readonly IHyperliquidAssetMetadataCache _metadataCache;
    private readonly HyperliquidOptions _options;
    private readonly ILogger<HyperliquidOrderService> _logger;

    public HyperliquidOrderService(
        IHyperliquidRestClient restClient,
        IHyperliquidSigner signer,
        INonceProvider nonceProvider,
        IHyperliquidAccountService accountService,
        IHyperliquidAssetMetadataCache metadataCache,
        IOptions<HyperliquidOptions> options,
        ILogger<HyperliquidOrderService> logger)
    {
        _restClient = restClient;
        _signer = signer;
        _nonceProvider = nonceProvider;
        _accountService = accountService;
        _metadataCache = metadataCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var coin = HyperliquidAssetMapper.ToCoin(request.Asset);
        var metadata = await _metadataCache.GetAsync(coin, cancellationToken);

        var isBuy = request.Side.Equals("buy", StringComparison.OrdinalIgnoreCase);
        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);
        var isMarket = request.OrderType.Equals("market", StringComparison.OrdinalIgnoreCase);

        decimal price;
        string tif;

        if (isMarket)
        {
            var midPrice = await GetMidPriceAsync(coin, cancellationToken);
            // 5% slippage for market orders: buy higher, sell lower
            var slippagePrice = isBuy ? midPrice * 1.05m : midPrice * 0.95m;
            price = RoundToSignificantFigures(slippagePrice, 5);
            tif = "Ioc";
            _logger.LogInformation(
                "Market order: Coin={Coin}, MidPrice={MidPrice}, SlippagePrice={SlippagePrice}, IsBuy={IsBuy}",
                coin, midPrice, price, isBuy);
        }
        else
        {
            price = request.Price ?? throw new DomainException("Price is required for limit orders.");
            tif = "Gtc";
        }

        var action = HyperliquidEip712.BuildOrderAction(
            assetIndex: metadata.Index,
            isBuy: isBuy,
            price: price,
            size: request.Size,
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

        var submitTimestamp = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var exchangeResponse = await _restClient
                .PostExchangeAsync<HyperliquidExchangeResponse>(payload, cancellationToken);
            stopwatch.Stop();

            var responseTimestamp = DateTimeOffset.UtcNow;
            _logger.LogInformation(
                "Order submitted. SubmitTimestampUtc={SubmitTimestampUtc}, ResponseTimestampUtc={ResponseTimestampUtc}, LatencyMs={LatencyMs}",
                submitTimestamp,
                responseTimestamp,
                stopwatch.ElapsedMilliseconds);

            var response = MapExchangeResponse(exchangeResponse);

            if (!response.Success)
            {
                return response;
            }

            await PlaceCompanionTriggerOrdersAsync(
                request,
                metadata.Index,
                !isBuy,
                response,
                cancellationToken);

            return response;
        }
        catch (HyperliquidApiException ex) when (
            ex.Message.Contains("signature", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("INVALID_SIGNATURE", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                ex,
                "EIP-712 signature rejected by Hyperliquid. WalletAddress={WalletAddress}, Nonce={Nonce}",
                _signer.WalletAddress,
                nonce);

            throw new SigningException(
                "Signature rejected — check signing configuration",
                ex);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(
                ex,
                "Order submission failed (network error). SubmitTimestampUtc={SubmitTimestampUtc}, LatencyMs={LatencyMs}",
                submitTimestamp,
                stopwatch.ElapsedMilliseconds);

            return new PlaceOrderResponse
            {
                Success = false,
                Status = "rejected",
                Detail = "Order submission failed. Please retry.",
            };
        }
    }

    public async Task<PlaceOrderResponse> PlaceTriggerOrderAsync(
        PlaceTriggerOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var assetIndex = await ResolveAssetIndexAsync(request.Asset, cancellationToken);
        var isBuy = request.Side.Equals("buy", StringComparison.OrdinalIgnoreCase);

        return await SubmitTriggerOrderAsync(
            assetIndex,
            isBuy,
            request.TriggerPrice,
            request.Size,
            request.TpslType,
            cancellationToken);
    }

    public Task<TestSignResponse> TestSignAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);
        var action = HyperliquidEip712.BuildOrderAction(
            assetIndex: 3,
            isBuy: true,
            price: 65000m,
            size: 0.001m);

        var nonce = _nonceProvider.GetNextNonce();
        var connectionId = HyperliquidEip712.ComputeActionHash(action, nonce, vaultAddress: null);
        var eip712Hash = HyperliquidEip712.ComputeEip712Hash(connectionId, isMainnet);
        var (r, s, v) = _signer.SignHash(eip712Hash);

        var messageHash = "0x" + Convert.ToHexString(connectionId).ToLowerInvariant();
        var eip712HashHex = "0x" + Convert.ToHexString(eip712Hash).ToLowerInvariant();

        return Task.FromResult(new TestSignResponse
        {
            DomainSeparator = eip712HashHex,
            TypeHash = messageHash,
            MessageHash = messageHash,
            Signature = new SignatureDto
            {
                V = v,
                R = r,
                S = s,
            },
        });
    }

    public async Task CancelOrderAsync(string orderId, string asset, CancellationToken cancellationToken = default)
    {
        var orderIdLong = ParseOrderId(orderId);
        var assetIndex = await ResolveAssetIndexAsync(asset, cancellationToken);

        var action = new HyperliquidCancelAction
        {
            Cancels =
            [
                new HyperliquidCancelEntry
                {
                    AssetIndex = assetIndex,
                    OrderId = orderIdLong,
                },
            ],
        };

        await SubmitExchangeActionAsync(action, cancellationToken);

        _logger.LogInformation("Cancelled order {OrderId} for asset {Asset}", orderId, asset);
    }

    public async Task CancelAllOrdersAsync(string asset, CancellationToken cancellationToken = default)
    {
        var normalizedAsset = NormalizeAsset(asset);
        var openOrders = await _accountService.GetOpenOrdersAsync(cancellationToken);
        var ordersForAsset = openOrders
            .Where(order => string.Equals(order.Asset, normalizedAsset, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (ordersForAsset.Count == 0)
        {
            _logger.LogInformation("No open orders to cancel for asset {Asset}", asset);
            return;
        }

        var assetIndex = await ResolveAssetIndexAsync(asset, cancellationToken);

        var action = new HyperliquidCancelAction
        {
            Cancels = ordersForAsset
                .Select(order => new HyperliquidCancelEntry
                {
                    AssetIndex = assetIndex,
                    OrderId = ParseOrderId(order.OrderId),
                })
                .ToList(),
        };

        await SubmitExchangeActionAsync(action, cancellationToken);

        _logger.LogInformation("Cancelled {Count} orders for asset {Asset}", ordersForAsset.Count, asset);
    }

    public async Task ModifyOrderAsync(
        string orderId,
        string asset,
        string side,
        decimal price,
        decimal size,
        CancellationToken cancellationToken = default)
    {
        var orderIdLong = ParseOrderId(orderId);
        var isBuy = side.Equals("buy", StringComparison.OrdinalIgnoreCase);
        var assetIndex = await ResolveAssetIndexAsync(asset, cancellationToken);

        var action = new HyperliquidModifyAction
        {
            Modifies =
            [
                new HyperliquidModifyEntry
                {
                    OrderId = orderIdLong,
                    Order = new HyperliquidModifyOrderParams
                    {
                        AssetIndex = assetIndex,
                        IsBuy = isBuy,
                        Price = ToWireDecimal(price),
                        Size = ToWireDecimal(size),
                        ReduceOnly = false,
                        OrderType = new HyperliquidOrderType
                        {
                            Limit = new HyperliquidLimitParams
                            {
                                Tif = "Gtc",
                            },
                        },
                    },
                },
            ],
        };

        await SubmitExchangeActionAsync(action, cancellationToken);

        _logger.LogInformation(
            "Modified order {OrderId} for asset {Asset}: price={Price}, size={Size}",
            orderId,
            asset,
            price,
            size);
    }

    public async Task ModifyTriggerOrderAsync(
        string orderId,
        string asset,
        string side,
        decimal triggerPrice,
        decimal size,
        string tpslType,
        CancellationToken cancellationToken = default)
    {
        var orderIdLong = ParseOrderId(orderId);
        var isBuy = side.Equals("buy", StringComparison.OrdinalIgnoreCase);
        var assetIndex = await ResolveAssetIndexAsync(asset, cancellationToken);

        var action = new HyperliquidModifyAction
        {
            Modifies =
            [
                new HyperliquidModifyEntry
                {
                    OrderId = orderIdLong,
                    Order = new HyperliquidModifyOrderParams
                    {
                        AssetIndex = assetIndex,
                        IsBuy = isBuy,
                        Price = ToWireDecimal(triggerPrice),
                        Size = ToWireDecimal(size),
                        ReduceOnly = true,
                        OrderType = new HyperliquidOrderType
                        {
                            Trigger = new HyperliquidTriggerParams
                            {
                                TriggerPx = ToWireDecimal(triggerPrice),
                                IsMarket = true,
                                Tpsl = tpslType,
                            },
                        },
                    },
                },
            ],
        };

        await SubmitExchangeActionAsync(action, cancellationToken);

        _logger.LogInformation(
            "Modified trigger order {OrderId} for asset {Asset}: triggerPrice={TriggerPrice}, size={Size}, tpslType={TpslType}",
            orderId,
            asset,
            triggerPrice,
            size,
            tpslType);
    }

    private async Task SubmitExchangeActionAsync(object action, CancellationToken cancellationToken)
    {
        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);
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

        try
        {
            var response = await _restClient.PostExchangeAsync<HyperliquidExchangeResponse>(payload, cancellationToken);

            if (response.Status == "err")
            {
                throw new DomainException(
                    response.Response?.ErrorMessage ?? "Exchange rejected the request");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Exchange action failed: {Message}", ex.Message);
            throw;
        }
    }

    private async Task<PlaceOrderResponse> SubmitTriggerOrderAsync(
        int assetIndex,
        bool isBuy,
        decimal triggerPrice,
        decimal size,
        string tpslType,
        CancellationToken cancellationToken)
    {
        var action = HyperliquidEip712.BuildTriggerOrderAction(
            assetIndex,
            isBuy,
            triggerPrice,
            size,
            tpslType);

        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);
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

        var exchangeResponse = await _restClient
            .PostExchangeAsync<HyperliquidExchangeResponse>(payload, cancellationToken);

        return MapExchangeResponse(exchangeResponse);
    }

    private async Task PlaceCompanionTriggerOrdersAsync(
        PlaceOrderRequest request,
        int assetIndex,
        bool closingIsBuy,
        PlaceOrderResponse response,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        if (request.StopLossPrice.HasValue)
        {
            try
            {
                var stopLossResponse = await SubmitTriggerOrderAsync(
                    assetIndex,
                    closingIsBuy,
                    request.StopLossPrice.Value,
                    request.Size,
                    "sl",
                    cancellationToken);

                if (!stopLossResponse.Success)
                {
                    warnings.Add($"Stop loss trigger order failed: {stopLossResponse.Detail ?? "Unknown exchange error"}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to place stop loss trigger order for {Asset}", request.Asset);
                warnings.Add($"Stop loss trigger order failed: {ex.Message}");
            }
        }

        if (request.TakeProfitPrice.HasValue)
        {
            try
            {
                var takeProfitResponse = await SubmitTriggerOrderAsync(
                    assetIndex,
                    closingIsBuy,
                    request.TakeProfitPrice.Value,
                    request.Size,
                    "tp",
                    cancellationToken);

                if (!takeProfitResponse.Success)
                {
                    warnings.Add($"Take profit trigger order failed: {takeProfitResponse.Detail ?? "Unknown exchange error"}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to place take profit trigger order for {Asset}", request.Asset);
                warnings.Add($"Take profit trigger order failed: {ex.Message}");
            }
        }

        if (warnings.Count > 0)
        {
            response.Detail = string.Join("; ", warnings);
        }
    }

    public async Task UpdateLeverageAsync(string asset, int leverage, bool isCross = true, CancellationToken cancellationToken = default)
    {
        var coin = NormalizeAsset(asset);
        var metadata = await _metadataCache.GetAsync(coin, cancellationToken);
        var assetIndex = metadata.Index;

        // Use Dictionary to ensure compact msgpack encoding (same as order actions)
        var action = new Dictionary<string, object>
        {
            ["type"] = "updateLeverage",
            ["asset"] = assetIndex,
            ["isCross"] = isCross,
            ["leverage"] = leverage,
        };

        await SubmitExchangeActionAsync(action, cancellationToken);

        _logger.LogInformation(
            "Updated leverage for {Asset}: leverage={Leverage}, isCross={IsCross}",
            asset, leverage, isCross);
    }

    private static long ParseOrderId(string orderId)
    {
        if (!long.TryParse(orderId, out var parsedOrderId))
        {
            throw new DomainException($"Invalid order id '{orderId}'.");
        }

        return parsedOrderId;
    }

    private async Task<int> ResolveAssetIndexAsync(string asset, CancellationToken cancellationToken)
    {
        var coin = NormalizeAsset(asset);
        var metadata = await _metadataCache.GetAsync(coin, cancellationToken);
        return metadata.Index;
    }

    private static string NormalizeAsset(string asset)
    {
        if (asset.EndsWith("-PERP", StringComparison.OrdinalIgnoreCase))
        {
            return HyperliquidAssetMapper.ToCoin(asset);
        }

        return asset;
    }

    private static string ToWireDecimal(decimal value)
    {
        var formatted = value.ToString("0.############################", CultureInfo.InvariantCulture);
        return formatted.Contains('.')
            ? formatted.TrimEnd('0').TrimEnd('.')
            : formatted;
    }

    private static PlaceOrderResponse MapExchangeResponse(HyperliquidExchangeResponse exchangeResponse)
    {
        if (exchangeResponse.Status == "err")
        {
            return new PlaceOrderResponse
            {
                Success = false,
                Status = "rejected",
                Detail = exchangeResponse.Response?.ErrorMessage ?? "Unknown exchange error",
            };
        }

        if (exchangeResponse.Status == "ok" &&
            exchangeResponse.Response?.Data?.Statuses is { Count: > 0 } statuses)
        {
            var first = statuses[0];

            if (first.Resting is not null)
            {
                return new PlaceOrderResponse
                {
                    Success = true,
                    OrderId = first.Resting.Oid.ToString(),
                    Status = "open",
                };
            }

            if (first.Filled is not null)
            {
                return new PlaceOrderResponse
                {
                    Success = true,
                    OrderId = first.Filled.Oid.ToString(),
                    Status = "filled",
                };
            }

            if (first.Error is not null)
            {
                return new PlaceOrderResponse
                {
                    Success = false,
                    Status = "rejected",
                    Detail = first.Error,
                };
            }
        }

        return new PlaceOrderResponse
        {
            Success = false,
            Status = "rejected",
            Detail = $"Unexpected response: {exchangeResponse.Status}",
        };
    }

    private async Task<decimal> GetMidPriceAsync(string coin, CancellationToken cancellationToken)
    {
        var request = new { type = "allMids" };
        var response = await _restClient.PostInfoAsync<JsonElement>(request, cancellationToken);

        _logger.LogDebug("allMids response type: {Kind}", response.ValueKind);

        if (response.TryGetProperty(coin, out var midElement))
        {
            var midStr = midElement.GetString();
            _logger.LogInformation("allMids[{Coin}] = {MidStr}", coin, midStr);
            if (midStr is not null && decimal.TryParse(midStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var mid))
            {
                return mid;
            }
        }

        throw new DomainException($"Could not retrieve mid price for {coin} from Hyperliquid.");
    }

    /// <summary>
    /// Rounds a decimal to the specified number of significant figures.
    /// Hyperliquid requires prices with max 5 significant figures.
    /// </summary>
    private static decimal RoundToSignificantFigures(decimal value, int significantFigures)
    {
        if (value == 0)
        {
            return 0;
        }

        var scale = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)Math.Abs(value))) + 1 - significantFigures);
        return scale * Math.Round(value / scale);
    }
}
