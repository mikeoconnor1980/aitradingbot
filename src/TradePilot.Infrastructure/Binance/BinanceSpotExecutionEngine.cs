using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceSpotExecutionEngine : IExecutionEngine
{
    private readonly IBinanceSpotAuthClient _authClient;
    private readonly IBinanceSpotExchangeInfoCache _exchangeInfoCache;
    private readonly ILogger<BinanceSpotExecutionEngine> _logger;
    private readonly ConcurrentDictionary<string, string> _orderAssetMap = new();

    public BinanceSpotExecutionEngine(
        IBinanceSpotAuthClient authClient,
        IBinanceSpotExchangeInfoCache exchangeInfoCache,
        ILogger<BinanceSpotExecutionEngine> logger)
    {
        _authClient = authClient;
        _exchangeInfoCache = exchangeInfoCache;
        _logger = logger;
    }

    public async Task<string> PlaceOrderAsync(OrderRequest order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var asset = BinanceAssetMapper.NormalizeSymbol(order.Symbol);
        var metadata = await GetSymbolMetadataAsync(asset, cancellationToken);
        var spotSymbol = BinanceAssetMapper.ToSpotSymbol(asset);
        var normalizedSize = NormalizeOrderSize(order.Size, metadata.SizeDecimals);
        if (normalizedSize == 0m)
        {
            throw new DomainException(
                $"Order size {order.Size} normalizes to zero for {asset} ({metadata.SizeDecimals} size decimals).");
        }

        decimal? normalizedPrice = null;
        if (order.OrderType == OrderType.Limit)
        {
            normalizedPrice = NormalizeOrderPrice(order.Price, metadata.PriceDecimals);
            if (normalizedPrice == 0m)
            {
                throw new DomainException(
                    $"Order price {order.Price} normalizes to zero for {asset} ({metadata.PriceDecimals} price decimals).");
            }
        }

        var result = await _authClient.PlaceOrderAsync(new BinanceSpotPlaceOrderRequest
        {
            Symbol = spotSymbol,
            Side = order.Side == OrderSide.Buy ? "BUY" : "SELL",
            Type = order.OrderType == OrderType.Market ? "MARKET" : "LIMIT",
            Quantity = normalizedSize,
            Price = normalizedPrice,
            TimeInForce = order.OrderType == OrderType.Limit ? "GTC" : null,
            NewOrderRespType = "RESULT",
        }, cancellationToken);

        var orderId = result.OrderId.ToString(CultureInfo.InvariantCulture);
        _orderAssetMap[orderId] = asset;
        _logger.LogInformation("Placed Binance Spot {OrderType} order {OrderId} for {Asset}", order.OrderType, orderId, asset);
        return orderId;
    }

    public async Task CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        if (!_orderAssetMap.TryGetValue(orderId, out var asset))
        {
            _logger.LogError(
                "Cannot resolve Binance Spot asset for order {OrderId}. Order may remain active on exchange.",
                orderId);
            throw new DomainException(
                $"Cannot cancel spot order {orderId}: asset mapping not found. The order may need to be cancelled manually via the exchange.");
        }

        await CancelOrderAsync(orderId, asset, cancellationToken);
    }

    public async Task CancelOrderAsync(string orderId, string asset, CancellationToken cancellationToken = default)
    {
        await _authClient.CancelOrderAsync(
            BinanceAssetMapper.ToSpotSymbol(asset),
            BinanceParsing.ParseOrderId(orderId),
            cancellationToken);

        _orderAssetMap.TryRemove(orderId, out _);
    }

    public async Task CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var asset = BinanceAssetMapper.NormalizeSymbol(symbol);
        await _authClient.CancelAllOrdersAsync(BinanceAssetMapper.ToSpotSymbol(asset), cancellationToken);

        foreach (var entry in _orderAssetMap.Where(entry => entry.Value.Equals(asset, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            _orderAssetMap.TryRemove(entry.Key, out _);
        }
    }

    public Task<string> PlaceTriggerOrderAsync(string asset, string side, decimal size, decimal triggerPrice, string tpslType, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Trigger orders (stop-loss / take-profit) are not supported on Binance Spot.");
    }

    public Task ModifyTriggerOrderAsync(string orderId, string asset, string side, decimal triggerPrice, decimal size, string tpslType, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Trigger orders (stop-loss / take-profit) are not supported on Binance Spot.");
    }

    public Task SetLeverageAsync(string asset, int leverage, bool isIsolated, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SetLeverageAsync is a no-op for Binance Spot (asset={Asset}).", asset);
        return Task.CompletedTask;
    }

    private async Task<BinanceSpotSymbolMetadata> GetSymbolMetadataAsync(string asset, CancellationToken cancellationToken)
    {
        return await _exchangeInfoCache.GetSymbolAsync(asset, cancellationToken)
            ?? throw new DomainException($"No spot exchange metadata found for asset '{asset}'.");
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

    private static decimal NormalizeOrderPrice(decimal price, int priceDecimals)
    {
        if (priceDecimals < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priceDecimals));
        }

        if (price == 0m)
        {
            return 0m;
        }

        var factor = (decimal)Math.Pow(10, priceDecimals);
        return decimal.Truncate(price * factor) / factor;
    }
}
