using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceExecutionEngine : IExecutionEngine, IPositionQueryable
{
    private readonly IBinanceFuturesAuthClient _authClient;
    private readonly ILogger<BinanceExecutionEngine> _logger;
    private readonly ConcurrentDictionary<string, string> _orderAssetMap = new();

    public BinanceExecutionEngine(
        IBinanceFuturesAuthClient authClient,
        ILogger<BinanceExecutionEngine> logger)
    {
        _authClient = authClient;
        _logger = logger;
    }

    public async Task<string> PlaceOrderAsync(OrderRequest order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var asset = BinanceAssetMapper.NormalizeSymbol(order.Symbol);
        var result = await _authClient.PlaceOrderAsync(new BinancePlaceOrderRequest
        {
            Symbol = BinanceAssetMapper.ToFuturesSymbol(asset),
            Side = order.Side == OrderSide.Buy ? "BUY" : "SELL",
            Type = order.OrderType == OrderType.Market ? "MARKET" : "LIMIT",
            Quantity = order.Size,
            Price = order.OrderType == OrderType.Limit ? order.Price : null,
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
            _logger.LogWarning("Cannot resolve Binance asset for order {OrderId}; skipping cancel without asset context.", orderId);
            return;
        }

        await CancelOrderAsync(orderId, asset, cancellationToken);
    }

    public async Task CancelOrderAsync(string orderId, string asset, CancellationToken cancellationToken = default)
    {
        await _authClient.CancelOrderAsync(
            BinanceAssetMapper.ToFuturesSymbol(asset),
            long.Parse(orderId, CultureInfo.InvariantCulture),
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
        var result = await _authClient.PlaceOrderAsync(new BinancePlaceOrderRequest
        {
            Symbol = BinanceAssetMapper.ToFuturesSymbol(normalizedAsset),
            Side = side.Equals("buy", StringComparison.OrdinalIgnoreCase) ? "BUY" : "SELL",
            Type = tpslType.Equals("tp", StringComparison.OrdinalIgnoreCase) ? "TAKE_PROFIT_MARKET" : "STOP_MARKET",
            Quantity = size,
            StopPrice = triggerPrice,
            ReduceOnly = true,
            WorkingType = "MARK_PRICE",
        }, cancellationToken);

        var orderId = result.OrderId.ToString(CultureInfo.InvariantCulture);
        _orderAssetMap[orderId] = normalizedAsset;
        return orderId;
    }

    public async Task ModifyTriggerOrderAsync(string orderId, string asset, string side, decimal triggerPrice, decimal size, string tpslType, CancellationToken cancellationToken = default)
    {
        await CancelOrderAsync(orderId, asset, cancellationToken);
        await PlaceTriggerOrderAsync(asset, side, size, triggerPrice, tpslType, cancellationToken);
    }

    public Task SetLeverageAsync(string asset, int leverage, bool isIsolated, CancellationToken cancellationToken = default)
        => _authClient.SetLeverageAsync(BinanceAssetMapper.ToFuturesSymbol(asset), leverage, cancellationToken);

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
            Size = ParseDecimal(position.PositionAmount),
            AverageEntryPrice = ParseDecimal(position.EntryPrice),
            UnrealisedPnL = ParseDecimal(position.UnrealizedProfit),
        };
    }

    public async Task<decimal> QueryAccountEquityAsync(CancellationToken cancellationToken = default)
    {
        var account = await _authClient.GetAccountAsync(cancellationToken);
        return ParseDecimal(account.TotalWalletBalance) + ParseDecimal(account.TotalUnrealizedProfit);
    }

    private static decimal ParseDecimal(string value)
        => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
}