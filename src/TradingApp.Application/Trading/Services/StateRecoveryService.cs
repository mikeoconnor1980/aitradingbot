using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Enums;

namespace TradingApp.Application.Trading.Services;

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
        if (openOrdersJson.ValueKind == JsonValueKind.Array)
        {
            foreach (var orderElement in openOrdersJson.EnumerateArray())
            {
                if (orderElement.TryGetProperty("oid", out var oidProp))
                {
                    openOrderIds.Add(oidProp.ToString());
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

        _logger.LogInformation(
            "Grid state recovered: GridCycleId={GridCycleId}, Lifecycle={Lifecycle}, " +
            "FilledLevels={Filled}/{Total}, TrackedOrders={OrderCount}",
            gridState.GridCycleId, gridState.Lifecycle,
            gridState.FilledLevels, gridState.TotalLevels, dbOrders.Count);

        return gridState;
    }
}
