using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Trading.Services;

public sealed class StateRecoveryService : IStateRecoveryService
{
    private readonly IGridCycleRepository _gridCycleRepository;
    private readonly ILiveOrderRepository _orderRepository;
    private readonly IHyperliquidRestClient _restClient;
    private readonly ILogger<StateRecoveryService> _logger;

    public StateRecoveryService(
        IGridCycleRepository gridCycleRepository,
        ILiveOrderRepository orderRepository,
        IHyperliquidRestClient restClient,
        ILogger<StateRecoveryService> logger)
    {
        _gridCycleRepository = gridCycleRepository;
        _orderRepository = orderRepository;
        _restClient = restClient;
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

        // Query Hyperliquid for current fills since cycle start
        var fills = await _restClient.GetUserFillsAsync(
            walletAddress,
            new DateTimeOffset(activeCycle.StartedAtUtc, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            cancellationToken);

        // Query Hyperliquid for open orders
        var openOrdersJson = await _restClient.PostInfoAsync<JsonElement>(
            new { type = "openOrders", user = walletAddress }, cancellationToken);

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

        if (openOrdersJson.ValueKind == JsonValueKind.Array)
        {
            foreach (var orderElement in openOrdersJson.EnumerateArray())
            {
                if (orderElement.TryGetProperty("oid", out var oidProp))
                {
                    openOrderIds.Add(oidProp.ToString());
                }

                // Detect exchange-native protection trigger orders for this symbol
                if (IsTriggerOrderForSymbol(orderElement, symbol))
                {
                    var tpslType = GetTpslType(orderElement);
                    var triggerPx = GetTriggerPrice(orderElement);
                    var oid = oidProp.ToString();

                    if (string.Equals(tpslType, "sl", StringComparison.OrdinalIgnoreCase))
                    {
                        recoveredSlOrderId = oid;
                        recoveredSlTriggerPrice = triggerPx;
                    }
                    else if (string.Equals(tpslType, "tp", StringComparison.OrdinalIgnoreCase))
                    {
                        recoveredTpOrderId = oid;
                        recoveredTpTriggerPrice = triggerPx;
                    }
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

    private static bool IsTriggerOrderForSymbol(JsonElement order, string symbol)
    {
        if (!order.TryGetProperty("coin", out var coinProp))
        {
            return false;
        }

        if (!string.Equals(coinProp.GetString(), symbol, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check if reduceOnly (all protection triggers are reduce-only)
        if (order.TryGetProperty("reduceOnly", out var reduceOnly) && reduceOnly.ValueKind == JsonValueKind.True)
        {
            return GetTpslType(order) is not null;
        }

        return false;
    }

    private static string? GetTpslType(JsonElement order)
    {
        if (order.TryGetProperty("orderType", out var orderType)
            && orderType.ValueKind == JsonValueKind.Object
            && orderType.TryGetProperty("trigger", out var trigger)
            && trigger.TryGetProperty("tpsl", out var tpsl))
        {
            return tpsl.GetString();
        }

        return null;
    }

    private static decimal? GetTriggerPrice(JsonElement order)
    {
        if (order.TryGetProperty("orderType", out var orderType)
            && orderType.ValueKind == JsonValueKind.Object
            && orderType.TryGetProperty("trigger", out var trigger)
            && trigger.TryGetProperty("triggerPx", out var triggerPx))
        {
            if (triggerPx.ValueKind == JsonValueKind.String
                && decimal.TryParse(triggerPx.GetString(), out var parsed))
            {
                return parsed;
            }

            if (triggerPx.ValueKind == JsonValueKind.Number)
            {
                return triggerPx.GetDecimal();
            }
        }

        return null;
    }
}
