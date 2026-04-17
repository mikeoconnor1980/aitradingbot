using System.Collections.Concurrent;
using TradePilot.Api.Models;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.Api.Services;

/// <summary>
/// Live <see cref="IExecutionEngine"/> implementation that delegates order placement,
/// cancellation, and bulk-cancel operations to the existing <see cref="IHyperliquidOrderService"/>.
/// Bridges the Application-layer <see cref="OrderRequest"/> model (enum-based) to the
/// Hyperliquid API contract (string-based).
/// </summary>
public sealed class HyperliquidExecutionEngine : IExecutionEngine
{
    private readonly IHyperliquidOrderService _orderService;
    private readonly ILogger<HyperliquidExecutionEngine> _logger;

    // Tracks orderId → asset so CancelOrderAsync can resolve the required asset parameter.
    private readonly ConcurrentDictionary<string, string> _orderAssetMap = new();

    public HyperliquidExecutionEngine(
        IHyperliquidOrderService orderService,
        ILogger<HyperliquidExecutionEngine> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    public async Task<string> PlaceOrderAsync(OrderRequest order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var request = new PlaceOrderRequest
        {
            Asset = order.Symbol,
            Side = MapSide(order.Side),
            OrderType = MapOrderType(order.OrderType),
            Price = order.OrderType == OrderType.Limit ? order.Price : null,
            Size = order.Size,
        };

        _logger.LogInformation(
            "Placing {OrderType} {Side} order: Symbol={Symbol}, Price={Price}, Size={Size}, TradeType={TradeType}, ClientOrderId={ClientOrderId}",
            order.OrderType, order.Side, order.Symbol, order.Price, order.Size, order.TradeType, order.ClientOrderId);

        var response = await _orderService.PlaceOrderAsync(request, cancellationToken);

        if (!response.Success)
        {
            _logger.LogWarning(
                "Order rejected by exchange: Symbol={Symbol}, Status={Status}, Detail={Detail}",
                order.Symbol, response.Status, response.Detail);

            return string.Empty;
        }

        var orderId = response.OrderId ?? string.Empty;

        if (!string.IsNullOrEmpty(orderId))
        {
            _orderAssetMap[orderId] = order.Symbol;
        }

        _logger.LogInformation(
            "Order placed successfully: OrderId={OrderId}, Symbol={Symbol}",
            orderId, order.Symbol);

        return orderId;
    }

    public async Task CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        if (!_orderAssetMap.TryGetValue(orderId, out var asset))
        {
            _logger.LogWarning(
                "Cannot cancel order {OrderId}: asset mapping not found. Order may have been placed externally.",
                orderId);
            return;
        }

        await CancelOrderAsync(orderId, asset, cancellationToken);
    }

    public async Task CancelOrderAsync(string orderId, string asset, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(asset);

        _logger.LogInformation("Cancelling order: OrderId={OrderId}, Asset={Asset}", orderId, asset);

        await _orderService.CancelOrderAsync(orderId, asset, cancellationToken);
        _orderAssetMap.TryRemove(orderId, out _);
    }

    public async Task CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        _logger.LogInformation("Cancelling all orders for symbol: {Symbol}", symbol);

        await _orderService.CancelAllOrdersAsync(symbol, cancellationToken);

        // Remove all tracked orders for this symbol
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

        var request = new PlaceTriggerOrderRequest
        {
            Asset = asset,
            Side = side,
            Size = size,
            TriggerPrice = triggerPrice,
            TpslType = tpslType,
        };

        _logger.LogInformation(
            "Placing trigger order: Asset={Asset}, Side={Side}, TriggerPrice={TriggerPrice}, Size={Size}, TpslType={TpslType}",
            asset, side, triggerPrice, size, tpslType);

        var response = await _orderService.PlaceTriggerOrderAsync(request, cancellationToken);
        return response.OrderId ?? string.Empty;
    }

    public async Task ModifyTriggerOrderAsync(string orderId, string asset, string side, decimal triggerPrice, decimal size, string tpslType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        _logger.LogInformation(
            "Modifying trigger order: OrderId={OrderId}, Asset={Asset}, TriggerPrice={TriggerPrice}",
            orderId, asset, triggerPrice);

        await _orderService.ModifyTriggerOrderAsync(orderId, asset, side, triggerPrice, size, tpslType, cancellationToken);
    }

    public Task SetLeverageAsync(string asset, int leverage, bool isIsolated, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(asset);
        ArgumentOutOfRangeException.ThrowIfLessThan(leverage, 1);

        return _orderService.UpdateLeverageAsync(asset, leverage, isCross: !isIsolated, cancellationToken);
    }

    private static string MapSide(OrderSide side) => side switch
    {
        OrderSide.Buy => "buy",
        OrderSide.Sell => "sell",
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unsupported order side.")
    };

    private static string MapOrderType(OrderType orderType) => orderType switch
    {
        OrderType.Limit => "limit",
        OrderType.Market => "market",
        _ => throw new ArgumentOutOfRangeException(nameof(orderType), orderType, "Unsupported order type.")
    };
}
