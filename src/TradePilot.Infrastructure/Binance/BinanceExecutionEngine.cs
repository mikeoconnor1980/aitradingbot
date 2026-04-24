using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceExecutionEngine : IExecutionEngine, IPositionQueryable
{
    private readonly IBinanceFuturesAuthClient _authClient;
    private readonly IBinanceExchangeInfoCache _exchangeInfoCache;
    private readonly ILogger<BinanceExecutionEngine> _logger;
    private readonly ConcurrentDictionary<string, string> _orderAssetMap = new();

    public BinanceExecutionEngine(
        IBinanceFuturesAuthClient authClient,
        IBinanceExchangeInfoCache exchangeInfoCache,
        ILogger<BinanceExecutionEngine> logger)
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

        var result = await _authClient.PlaceOrderAsync(new BinancePlaceOrderRequest
        {
            Symbol = BinanceAssetMapper.ToFuturesSymbol(asset),
            Side = order.Side == OrderSide.Buy ? "BUY" : "SELL",
            Type = order.OrderType == OrderType.Market ? "MARKET" : "LIMIT",
            Quantity = normalizedSize,
            Price = normalizedPrice,
            TimeInForce = order.OrderType == OrderType.Limit ? "GTC" : null,
            ReduceOnly = order.ReduceOnly,
        }, cancellationToken);

        var orderId = result.OrderId.ToString(CultureInfo.InvariantCulture);
        _orderAssetMap[orderId] = asset;
        _logger.LogInformation("Placed Binance {OrderType} order {OrderId} for {Asset}", order.OrderType, orderId, asset);
        return orderId;
    }

    public async Task CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        if (!_orderAssetMap.TryGetValue(orderId, out var asset))
        {
            _logger.LogError(
                "Cannot resolve Binance asset for order {OrderId}. Order may remain active on exchange. This can happen after process restart because the in-memory order map does not persist.",
                orderId);
            throw new DomainException(
                $"Cannot cancel order {orderId}: asset mapping not found. The order may need to be cancelled manually via the exchange.");
        }

        await CancelOrderAsync(orderId, asset, cancellationToken);
    }

    public async Task CancelOrderAsync(string orderId, string asset, CancellationToken cancellationToken = default)
    {
        await _authClient.CancelOrderAsync(
            BinanceAssetMapper.ToFuturesSymbol(asset),
            BinanceParsing.ParseOrderId(orderId),
            cancellationToken);

        _orderAssetMap.TryRemove(orderId, out _);
    }

    public async Task CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var asset = BinanceAssetMapper.NormalizeSymbol(symbol);
        await _authClient.CancelAllOrdersAsync(BinanceAssetMapper.ToFuturesSymbol(asset), cancellationToken);

        foreach (var entry in _orderAssetMap.Where(entry => entry.Value.Equals(asset, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            _orderAssetMap.TryRemove(entry.Key, out _);
        }
    }

    public async Task<string> PlaceTriggerOrderAsync(string asset, string side, decimal size, decimal triggerPrice, string tpslType, CancellationToken cancellationToken = default)
    {
        var normalizedAsset = BinanceAssetMapper.NormalizeSymbol(asset);
        var metadata = await GetSymbolMetadataAsync(normalizedAsset, cancellationToken);
        var normalizedSize = NormalizeOrderSize(size, metadata.SizeDecimals);
        if (normalizedSize == 0m)
        {
            throw new DomainException(
                $"Order size {size} normalizes to zero for {normalizedAsset} ({metadata.SizeDecimals} size decimals).");
        }

        var normalizedTriggerPrice = NormalizeOrderPrice(triggerPrice, metadata.PriceDecimals);
        if (normalizedTriggerPrice == 0m)
        {
            throw new DomainException(
                $"Trigger price {triggerPrice} normalizes to zero for {normalizedAsset} ({metadata.PriceDecimals} price decimals).");
        }

        var result = await _authClient.PlaceOrderAsync(new BinancePlaceOrderRequest
        {
            Symbol = BinanceAssetMapper.ToFuturesSymbol(normalizedAsset),
            Side = side.Equals("buy", StringComparison.OrdinalIgnoreCase) ? "BUY" : "SELL",
            Type = tpslType.Equals("tp", StringComparison.OrdinalIgnoreCase) ? "TAKE_PROFIT_MARKET" : "STOP_MARKET",
            Quantity = normalizedSize,
            StopPrice = normalizedTriggerPrice,
            ReduceOnly = true,
            WorkingType = "MARK_PRICE",
        }, cancellationToken);

        var orderId = result.OrderId.ToString(CultureInfo.InvariantCulture);
        _orderAssetMap[orderId] = normalizedAsset;
        return orderId;
    }

    public async Task ModifyTriggerOrderAsync(string orderId, string asset, string side, decimal triggerPrice, decimal size, string tpslType, CancellationToken cancellationToken = default)
    {
        var normalizedAsset = BinanceAssetMapper.NormalizeSymbol(asset);
        await CancelOrderAsync(orderId, normalizedAsset, cancellationToken);

        try
        {
            var replacementOrderId = await PlaceTriggerOrderAsync(normalizedAsset, side, size, triggerPrice, tpslType, cancellationToken);
            _logger.LogInformation(
                "Replaced Binance {TpslType} trigger order {OldOrderId} with {NewOrderId} for {Asset}.",
                tpslType,
                orderId,
                replacementOrderId,
                normalizedAsset);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to place replacement trigger order for {Asset} after cancelling {OrderId}. Position may be unprotected. Attempting recovery placement.",
                normalizedAsset,
                orderId);

            try
            {
                var recoveryOrderId = await PlaceTriggerOrderAsync(normalizedAsset, side, size, triggerPrice, tpslType, cancellationToken);
                _logger.LogWarning(
                    "Recovery trigger order {RecoveryOrderId} placed successfully for {Asset} after replace failure.",
                    recoveryOrderId,
                    normalizedAsset);
            }
            catch (Exception retryEx)
            {
                _logger.LogCritical(
                    retryEx,
                    "CRITICAL: Recovery trigger order also failed for {Asset}. Position has no {TpslType} protection. Manual intervention required.",
                    normalizedAsset,
                    tpslType);

                throw new DomainException(
                    $"Failed to modify trigger order for {normalizedAsset}: cancel succeeded but replacement failed twice. Position has no {tpslType} protection. Initial error: {ex.Message}. Recovery error: {retryEx.Message}");
            }
        }
    }

    public async Task SetLeverageAsync(string asset, int leverage, bool isIsolated, CancellationToken cancellationToken = default)
    {
        var normalizedAsset = BinanceAssetMapper.NormalizeSymbol(asset);
        var symbol = BinanceAssetMapper.ToFuturesSymbol(normalizedAsset);

        await _authClient.SetMarginTypeAsync(symbol, isIsolated, cancellationToken);
        await _authClient.SetLeverageAsync(symbol, leverage, cancellationToken);
    }

    public async Task<PositionState> QueryPositionAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var asset = BinanceAssetMapper.NormalizeSymbol(symbol);
        var position = (await _authClient.GetPositionRiskAsync(BinanceAssetMapper.ToFuturesSymbol(asset), cancellationToken))
            .FirstOrDefault();

        if (position is null)
        {
            return new PositionState { Symbol = asset };
        }

        return new PositionState
        {
            Symbol = asset,
            Size = BinanceParsing.ParseDecimal(position.PositionAmount),
            AverageEntryPrice = BinanceParsing.ParseDecimal(position.EntryPrice),
            UnrealisedPnL = BinanceParsing.ParseDecimal(position.UnrealizedProfit),
        };
    }

    public async Task<decimal> QueryAccountEquityAsync(CancellationToken cancellationToken = default)
    {
        var account = await _authClient.GetAccountAsync(cancellationToken);
        return BinanceParsing.ParseDecimal(account.TotalWalletBalance) + BinanceParsing.ParseDecimal(account.TotalUnrealizedProfit);
    }

    private async Task<BinanceExchangeSymbolMetadata> GetSymbolMetadataAsync(string asset, CancellationToken cancellationToken)
    {
        return await _exchangeInfoCache.GetSymbolAsync(asset, cancellationToken)
            ?? throw new DomainException($"No exchange metadata found for asset '{asset}'.");
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