using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Trading.Services;

public sealed class FillProcessor : IFillProcessor
{
    private readonly IOrderTracker _orderTracker;
    private readonly GridState _gridState;
    private readonly IRiskEngine? _riskEngine;
    private readonly ILiveOrderRepository? _orderRepository;
    private readonly ILiveFillRepository? _fillRepository;
    private readonly IGridCycleRepository? _gridCycleRepository;
    private readonly IExecutionEngine? _executionEngine;
    private readonly string _userId;
    private readonly ILogger<FillProcessor> _logger;

    /// <summary>
    /// Optional callback invoked after a fill is processed.
    /// Used by TradingSession to update PositionState on the StrategyScheduler.
    /// </summary>
    public Func<FillEventDto, Task>? OnFillProcessed { get; set; }

    public FillProcessor(
        IOrderTracker orderTracker,
        GridState gridState,
        ILogger<FillProcessor> logger,
        IRiskEngine? riskEngine = null,
        ILiveOrderRepository? orderRepository = null,
        ILiveFillRepository? fillRepository = null,
        IGridCycleRepository? gridCycleRepository = null,
        string? userId = null,
        IExecutionEngine? executionEngine = null)
    {
        _orderTracker = orderTracker ?? throw new ArgumentNullException(nameof(orderTracker));
        _gridState = gridState ?? throw new ArgumentNullException(nameof(gridState));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _riskEngine = riskEngine!;
        _orderRepository = orderRepository;
        _fillRepository = fillRepository;
        _gridCycleRepository = gridCycleRepository;
        _executionEngine = executionEngine;
        _userId = userId ?? string.Empty;
    }

    public async Task ProcessFillAsync(FillEventDto fill, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fill);

        // Check if this fill is from an exchange-native protection trigger order
        if (_gridState.ProtectionOrders.IsProtectionOrderId(fill.OrderId))
        {
            ProcessProtectionTriggerFill(fill);
            await PersistProtectionFillAsync(fill, cancellationToken);
            _riskEngine?.RecordOrdersClosed(1);
            _riskEngine?.RecordPositionClosed(fill.Asset);
            if (fill.ClosedPnl < 0m)
            {
                _riskEngine?.RecordLoss(Math.Abs(fill.ClosedPnl));
            }

            var onFillProcessed = OnFillProcessed;
            if (onFillProcessed is not null)
            {
                await onFillProcessed(fill);
            }
            return;
        }

        var tracked = _orderTracker.GetOrder(fill.OrderId);

        if (tracked is null)
        {
            _logger.LogDebug(
                "Fill received for untracked order: OrderId={OrderId}, Asset={Asset}, Size={Size}",
                fill.OrderId, fill.Asset, fill.Size);
            return;
        }

        _logger.LogInformation(
            "Fill received: OrderId={OrderId}, Asset={Asset}, Side={Side}, Price={Price}, Size={Size}, " +
            "GridCycleId={GridCycleId}, Level={Level}, TradeType={TradeType}",
            fill.OrderId, fill.Asset, fill.Side, fill.Price, fill.Size,
            tracked.GridCycleId, tracked.Level, tracked.TradeType);

        tracked.Status = TrackedOrderStatus.Filled;

        if (tracked.TradeType == TradeType.GridFill)
        {
            ProcessGridFill(tracked);
        }
        else if (tracked.TradeType == TradeType.TakeProfit)
        {
            ProcessTakeProfitFill(tracked, fill);
        }
        else if (tracked.TradeType is TradeType.SignalEntry or TradeType.DcaBuy)
        {
            ProcessSignalEntryFill(tracked);
        }

        await PersistFillAsync(fill, tracked, cancellationToken);

        // Notify RiskEngine of fill
        _riskEngine?.RecordOrdersClosed(1);
        if (fill.ClosedPnl < 0m)
        {
            _riskEngine?.RecordLoss(Math.Abs(fill.ClosedPnl));
        }

        // Notify TradingSession to update PositionState
        var processedCallback = OnFillProcessed;
        if (processedCallback is not null)
        {
            await processedCallback(fill);
        }

        return;
    }

    public Task ProcessOrderUpdateAsync(OrderUpdateDto update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var tracked = _orderTracker.GetOrder(update.OrderId);

        if (tracked is null)
        {
            _logger.LogDebug(
                "Order update for untracked order: OrderId={OrderId}, Status={Status}",
                update.OrderId, update.Status);
            return Task.CompletedTask;
        }

        if (string.Equals(update.Status, "canceled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(update.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
        {
            tracked.Status = TrackedOrderStatus.Cancelled;

            _logger.LogInformation(
                "Order cancelled: OrderId={OrderId}, GridCycleId={GridCycleId}, Level={Level}",
                update.OrderId, tracked.GridCycleId, tracked.Level);

            _riskEngine?.RecordOrdersClosed(1);
        }

        return Task.CompletedTask;
    }

    private void ProcessGridFill(TrackedOrder tracked)
    {
        lock (_gridState.SyncRoot)
        {
            if (_gridState.Lifecycle is not (GridLifecycle.Deploying or GridLifecycle.Active or GridLifecycle.PartiallyFilled))
            {
                _logger.LogWarning(
                    "Grid fill received in unexpected lifecycle state: {Lifecycle}, OrderId={OrderId}",
                    _gridState.Lifecycle, tracked.OrderId);
                return;
            }

            _gridState.FilledLevels = Math.Min(_gridState.TotalLevels, _gridState.FilledLevels + 1);

            if (_gridState.Lifecycle == GridLifecycle.Deploying)
            {
                _gridState.Lifecycle = GridLifecycle.Active;
            }

            _gridState.Lifecycle = _gridState.FilledLevels >= _gridState.TotalLevels
                ? GridLifecycle.FullyFilled
                : GridLifecycle.PartiallyFilled;
        }

        _logger.LogInformation(
            "Grid fill processed: FilledLevels={Filled}/{Total}, Lifecycle={Lifecycle}, GridCycleId={GridCycleId}",
            _gridState.FilledLevels, _gridState.TotalLevels, _gridState.Lifecycle, _gridState.GridCycleId);
    }

    private void ProcessTakeProfitFill(TrackedOrder tracked, FillEventDto fill)
    {
        lock (_gridState.SyncRoot)
        {
            _gridState.Lifecycle = GridLifecycle.Closed;
            _gridState.FilledLevels = 0;
            _gridState.TotalLevels = 0;
            _gridState.TrailingStopHighWatermark = null;
            _gridState.CandlesSinceEntry = 0;
            _gridState.ProtectionOrders.Clear();
        }

        _riskEngine?.RecordPositionClosed(fill.Asset);

        _logger.LogInformation(
            "Take profit fill processed: GridCycleId={GridCycleId} → Closed, ClosedPnl={ClosedPnl}",
            tracked.GridCycleId, fill.ClosedPnl);
    }

    private void ProcessProtectionTriggerFill(FillEventDto fill)
    {
        var isStopLoss = string.Equals(fill.OrderId, _gridState.ProtectionOrders.StopLossOrderId, StringComparison.Ordinal);
        var label = isStopLoss ? "SL" : "TP";

        // Identify the counterpart order to cancel (SL fired → cancel TP, and vice versa)
        var counterpartOrderId = isStopLoss
            ? _gridState.ProtectionOrders.TakeProfitOrderId
            : _gridState.ProtectionOrders.StopLossOrderId;

        _logger.LogInformation(
            "Exchange-native {Label} trigger filled: OrderId={OrderId}, Price={Price}, Size={Size}, ClosedPnl={ClosedPnl}",
            label, fill.OrderId, fill.Price, fill.Size, fill.ClosedPnl);

        lock (_gridState.SyncRoot)
        {
            _gridState.Lifecycle = GridLifecycle.Closed;
            _gridState.FilledLevels = 0;
            _gridState.TotalLevels = 0;
            _gridState.TrailingStopHighWatermark = null;
            _gridState.CandlesSinceEntry = 0;
            _gridState.ProtectionOrders.Clear();
        }

        // Cancel the counterpart protection order on the exchange (best-effort, fire-and-forget)
        if (!string.IsNullOrEmpty(counterpartOrderId) && _executionEngine is not null)
        {
            _ = CancelCounterpartOrderAsync(counterpartOrderId);
        }
    }

    private async Task CancelCounterpartOrderAsync(string orderId)
    {
        try
        {
            _logger.LogInformation(
                "Cancelling counterpart protection trigger: OrderId={OrderId}", orderId);
            await _executionEngine!.CancelOrderAsync(orderId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to cancel counterpart protection trigger: OrderId={OrderId}. May have already fired.",
                orderId);
        }
    }

    private async Task PersistProtectionFillAsync(FillEventDto fill, CancellationToken cancellationToken)
    {
        try
        {
            if (_fillRepository is not null)
            {
                await _fillRepository.AddAsync(new LiveFill
                {
                    Id = Guid.NewGuid(),
                    OrderId = fill.OrderId,
                    Symbol = fill.Asset,
                    Side = Enum.TryParse<OrderSide>(fill.Side, ignoreCase: true, out var side) ? side : OrderSide.Sell,
                    Direction = fill.Direction,
                    Price = fill.Price,
                    Size = fill.Size,
                    Fee = fill.Fee,
                    ClosedPnl = fill.ClosedPnl,
                    FilledAtUtc = fill.Timestamp,
                    UserId = _userId,
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to persist protection trigger fill: OrderId={OrderId}. Trading continues.",
                fill.OrderId);
        }
    }

    private void ProcessSignalEntryFill(TrackedOrder tracked)
    {
        _logger.LogInformation(
            "Signal entry fill processed: OrderId={OrderId}, Symbol={Symbol}",
            tracked.OrderId, tracked.Symbol);
    }

    private async Task PersistFillAsync(FillEventDto fill, TrackedOrder tracked, CancellationToken cancellationToken)
    {
        try
        {
            if (_fillRepository is not null)
            {
                await _fillRepository.AddAsync(new LiveFill
                {
                    Id = Guid.NewGuid(),
                    OrderId = fill.OrderId,
                    Symbol = fill.Asset,
                    Side = Enum.TryParse<OrderSide>(fill.Side, ignoreCase: true, out var side) ? side : OrderSide.Buy,
                    Direction = fill.Direction,
                    Price = fill.Price,
                    Size = fill.Size,
                    Fee = fill.Fee,
                    ClosedPnl = fill.ClosedPnl,
                    FilledAtUtc = fill.Timestamp,
                    UserId = _userId,
                }, cancellationToken);
            }

            if (_orderRepository is not null)
            {
                var dbOrder = await _orderRepository.GetByOrderIdAsync(fill.OrderId, cancellationToken);
                if (dbOrder is not null)
                {
                    dbOrder.Status = OrderStatus.Filled;
                    dbOrder.FilledAtUtc = fill.Timestamp;
                    await _orderRepository.UpdateAsync(dbOrder, cancellationToken);
                }
            }

            if (_gridCycleRepository is not null && tracked.GridCycleId is not "signal" and not "default")
            {
                var cycle = await _gridCycleRepository.GetByGridCycleIdAsync(tracked.GridCycleId, cancellationToken);
                if (cycle is not null)
                {
                    cycle.FilledLevels = _gridState.FilledLevels;
                    cycle.Lifecycle = _gridState.Lifecycle.ToString();

                    if (_gridState.Lifecycle is GridLifecycle.Closed or GridLifecycle.FullyFilled)
                    {
                        cycle.ClosedAtUtc = DateTime.UtcNow;
                    }

                    cycle.RealisedPnl = (cycle.RealisedPnl ?? 0m) + fill.ClosedPnl;

                    await _gridCycleRepository.UpdateAsync(cycle, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to persist fill data: OrderId={OrderId}. Trading continues.",
                fill.OrderId);
        }
    }
}
