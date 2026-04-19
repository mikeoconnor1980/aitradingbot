using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Application.Trading.Services;

public sealed class StateRecoveryService : IStateRecoveryService
{
    private readonly IGridCycleRepository _gridCycleRepository;
    private readonly ILiveOrderRepository _orderRepository;
    private readonly IExchangeAccountClient _accountClient;
    private readonly ILogger<StateRecoveryService> _logger;

    public StateRecoveryService(
        IGridCycleRepository gridCycleRepository,
        ILiveOrderRepository orderRepository,
        IExchangeAccountClient accountClient,
        ILogger<StateRecoveryService> logger)
    {
        _gridCycleRepository = gridCycleRepository;
        _orderRepository = orderRepository;
        _accountClient = accountClient;
        _logger = logger;
    }

    public async Task<GridState> RecoverAsync(
        string strategyName,
        string symbol,
        string walletAddress,
        IOrderTracker orderTracker,
        CancellationToken cancellationToken = default)
    {
        var activeCycle = await _gridCycleRepository.GetActiveForStrategyAsync(
            strategyName, symbol, cancellationToken);

        if (activeCycle is null)
        {
            _logger.LogInformation(
                "No active grid cycle found for {Strategy}/{Symbol}. Starting fresh.",
                strategyName, symbol);
            return new GridState();
        }

        _logger.LogInformation(
            "Recovering grid cycle: GridCycleId={GridCycleId}, Lifecycle={Lifecycle}, " +
            "FilledLevels={Filled}/{Total}",
            activeCycle.GridCycleId, activeCycle.Lifecycle,
            activeCycle.FilledLevels, activeCycle.TotalLevels);

        var pair = ToTradingPair(symbol);

        // Query Hyperliquid for current fills since cycle start
        var fills = await _accountClient.GetRecentFillsAsync(pair, walletAddress, cancellationToken);

        // Query Hyperliquid for open orders
        var openOrders = await _accountClient.GetOpenOrdersAsync(walletAddress, cancellationToken);

        // Rebuild order tracker from DB records
        var dbOrders = await _orderRepository.GetByGridCycleIdAsync(
            activeCycle.GridCycleId, cancellationToken);

        var filledOrderIds = fills
            .Select(f => f.OrderId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var openOrderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? recoveredSlOrderId = null;
        decimal? recoveredSlTriggerPrice = null;
        string? recoveredTpOrderId = null;
        decimal? recoveredTpTriggerPrice = null;

        foreach (var openOrder in openOrders)
        {
            openOrderIds.Add(openOrder.OrderId);

            if (IsTriggerOrderForSymbol(openOrder, pair.Base))
            {
                if (string.Equals(openOrder.TpslType, "sl", StringComparison.OrdinalIgnoreCase))
                {
                    recoveredSlOrderId = openOrder.OrderId;
                    recoveredSlTriggerPrice = openOrder.TriggerPrice;
                }
                else if (string.Equals(openOrder.TpslType, "tp", StringComparison.OrdinalIgnoreCase))
                {
                    recoveredTpOrderId = openOrder.OrderId;
                    recoveredTpTriggerPrice = openOrder.TriggerPrice;
                }
            }
        }

        var recoveredFilledLevels = 0;

        foreach (var dbOrder in dbOrders)
        {
            var side = dbOrder.Side == OrderSide.Buy ? OrderSide.Buy : OrderSide.Sell;
            var tradeType = Enum.TryParse<TradeType>(dbOrder.TradeType, out var tt) ? tt : TradeType.GridFill;

            orderTracker.TrackOrder(
                dbOrder.OrderId,
                dbOrder.GridCycleId,
                dbOrder.Level,
                dbOrder.Symbol,
                side,
                dbOrder.Price,
                dbOrder.Size,
                tradeType);

            if (filledOrderIds.Contains(dbOrder.OrderId))
            {
                var tracked = orderTracker.GetOrder(dbOrder.OrderId);
                if (tracked is not null)
                {
                    tracked.Status = TrackedOrderStatus.Filled;
                }

                if (tradeType == TradeType.GridFill)
                {
                    recoveredFilledLevels++;
                }
            }
            else if (!openOrderIds.Contains(dbOrder.OrderId))
            {
                // Not filled and not open — must be cancelled
                var tracked = orderTracker.GetOrder(dbOrder.OrderId);
                if (tracked is not null)
                {
                    tracked.Status = TrackedOrderStatus.Cancelled;
                }
            }
        }

        var lifecycle = Enum.TryParse<GridLifecycle>(activeCycle.Lifecycle, out var lc)
            ? lc
            : GridLifecycle.Active;

        var gridState = new GridState
        {
            GridCycleId = activeCycle.GridCycleId,
            TotalLevels = activeCycle.TotalLevels,
            FilledLevels = Math.Max(activeCycle.FilledLevels, recoveredFilledLevels),
            Lifecycle = lifecycle,
        };

        // Adjust lifecycle based on actual fill count
        if (gridState.FilledLevels >= gridState.TotalLevels && lifecycle is not (GridLifecycle.Closing or GridLifecycle.Closed))
        {
            gridState.Lifecycle = GridLifecycle.FullyFilled;
        }
        else if (gridState.FilledLevels > 0 && lifecycle == GridLifecycle.Active)
        {
            gridState.Lifecycle = GridLifecycle.PartiallyFilled;
        }

        // Recover protection order state from exchange open orders
        if (recoveredSlOrderId is not null || recoveredTpOrderId is not null)
        {
            gridState.ProtectionOrders.StopLossOrderId = recoveredSlOrderId;
            gridState.ProtectionOrders.StopLossTriggerPrice = recoveredSlTriggerPrice;
            gridState.ProtectionOrders.TakeProfitOrderId = recoveredTpOrderId;
            gridState.ProtectionOrders.TakeProfitTriggerPrice = recoveredTpTriggerPrice;

            _logger.LogInformation(
                "Protection orders recovered: SL={SlOrderId} @ {SlPrice}, TP={TpOrderId} @ {TpPrice}",
                recoveredSlOrderId ?? "(none)", recoveredSlTriggerPrice,
                recoveredTpOrderId ?? "(none)", recoveredTpTriggerPrice);
        }

        _logger.LogInformation(
            "Grid state recovered: GridCycleId={GridCycleId}, Lifecycle={Lifecycle}, " +
            "FilledLevels={Filled}/{Total}, TrackedOrders={OrderCount}",
            gridState.GridCycleId, gridState.Lifecycle,
            gridState.FilledLevels, gridState.TotalLevels, dbOrders.Count);

        return gridState;
    }

    private static bool IsTriggerOrderForSymbol(OpenOrderDto order, string symbol)
    {
        if (!string.Equals(order.Asset, symbol, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return order.IsReduceOnly && !string.IsNullOrWhiteSpace(order.TpslType);
    }

    private static TradingPair ToTradingPair(string symbol)
    {
        var normalized = symbol.EndsWith("-PERP", StringComparison.OrdinalIgnoreCase)
            ? symbol[..^5]
            : symbol;

        return TradingPair.Create(normalized, "USD", AssetType.Perp);
    }
}
