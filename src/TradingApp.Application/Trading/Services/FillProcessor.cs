using Microsoft.Extensions.Logging;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;

namespace TradingApp.Application.Trading.Services;

public sealed class FillProcessor : IFillProcessor
{
    private readonly IOrderTracker _orderTracker;
    private readonly GridState _gridState;
    private readonly IRiskEngine _riskEngine;
    private readonly ILiveOrderRepository? _orderRepository;
    private readonly ILiveFillRepository? _fillRepository;
    private readonly IGridCycleRepository? _gridCycleRepository;
    private readonly string _userId;
    private readonly ILogger<FillProcessor> _logger;

    /// <summary>
    /// Optional callback invoked after a fill is processed.
    /// Used by TradingSession to update PositionState on the StrategyScheduler.
    /// </summary>
    public Action<FillEventDto>? OnFillProcessed { get; set; }

    public FillProcessor(
        IOrderTracker orderTracker,
        GridState gridState,
        ILogger<FillProcessor> logger,
        IRiskEngine? riskEngine = null,
        ILiveOrderRepository? orderRepository = null,
        ILiveFillRepository? fillRepository = null,
        IGridCycleRepository? gridCycleRepository = null,
        string? userId = null)
    {
        _orderTracker = orderTracker ?? throw new ArgumentNullException(nameof(orderTracker));
        _gridState = gridState ?? throw new ArgumentNullException(nameof(gridState));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _riskEngine = riskEngine!;
        _orderRepository = orderRepository;
        _fillRepository = fillRepository;
        _gridCycleRepository = gridCycleRepository;
        _userId = userId ?? string.Empty;
    }

    public async Task ProcessFillAsync(FillEventDto fill, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fill);

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
        else if (tracked.TradeType == TradeType.SignalEntry)
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
        OnFillProcessed?.Invoke(fill);

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
        }

        _logger.LogInformation(
            "Take profit fill processed: GridCycleId={GridCycleId} → Closed, ClosedPnl={ClosedPnl}",
            tracked.GridCycleId, fill.ClosedPnl);
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
