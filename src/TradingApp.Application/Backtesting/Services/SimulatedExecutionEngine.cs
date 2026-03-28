using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// In-memory execution engine used by backtests.
/// </summary>
public sealed class SimulatedExecutionEngine : IExecutionEngine
{
    private readonly FeeModel _feeModel;
    private readonly List<SimulatedOrder> _openOrders = [];
    private readonly List<SimulatedFill> _allFills = [];
    private readonly SimulatedPosition _position = new();
    private int _orderCounter;
    private long _currentTimestampUtc;

    public SimulatedExecutionEngine(FeeModel feeModel)
    {
        _feeModel = feeModel ?? throw new ArgumentNullException(nameof(feeModel));
    }

    public Task<string> PlaceOrderAsync(OrderRequest order, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(order);

        var orderId = $"SIM-{++_orderCounter:D6}";

        _openOrders.Add(new SimulatedOrder
        {
            OrderId = orderId,
            Symbol = order.Symbol,
            Side = order.Side,
            OrderType = order.OrderType,
            Price = order.Price,
            Size = order.Size,
            TradeType = order.TradeType,
            PlacedAtUtc = _currentTimestampUtc
        });

        return Task.FromResult(orderId);
    }

    public Task CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        _openOrders.RemoveAll(order => string.Equals(order.OrderId, orderId, StringComparison.Ordinal));
        return Task.CompletedTask;
    }

    public Task CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        _openOrders.RemoveAll(order => string.Equals(order.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public IReadOnlyList<SimulatedFill> ProcessCandle(Candle candle)
    {
        ArgumentNullException.ThrowIfNull(candle);

        _currentTimestampUtc = candle.Timestamp;
        var candleFills = new List<SimulatedFill>();
        var filledOrderIds = new HashSet<string>(StringComparer.Ordinal);

        var orderedOrders = _openOrders
            .OrderBy(order => order.Side == OrderSide.Buy ? 0 : 1)
            .ToList();

        foreach (var order in orderedOrders)
        {
            if (!string.Equals(order.Symbol, candle.Symbol, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fill = TryFillOrder(order, candle);
            if (fill is null)
            {
                continue;
            }

            candleFills.Add(fill);
            filledOrderIds.Add(order.OrderId);
            UpdatePosition(fill);
        }

        _openOrders.RemoveAll(order => filledOrderIds.Contains(order.OrderId));

        if (candleFills.Count > 0)
        {
            _allFills.AddRange(candleFills);
        }

        UpdateUnrealisedPnl(candle);

        return candleFills;
    }

    public IReadOnlyList<SimulatedOrder> GetOpenOrders() => _openOrders.AsReadOnly();

    public SimulatedPosition GetPosition() => _position;

    public IReadOnlyList<SimulatedFill> GetAllFills() => _allFills.AsReadOnly();

    private SimulatedFill? TryFillOrder(SimulatedOrder order, Candle candle)
    {
        return order switch
        {
            { Side: OrderSide.Buy, OrderType: OrderType.Limit } when candle.Low <= order.Price
                => CreateFill(order, candle.Timestamp, order.Price, isMaker: true),

            { Side: OrderSide.Sell, OrderType: OrderType.Limit } when candle.High >= order.Price
                => CreateFill(order, candle.Timestamp, order.Price, isMaker: true),

            { OrderType: OrderType.Market }
                => CreateFill(order, candle.Timestamp, candle.Close, isMaker: false),

            _ => null
        };
    }

    private SimulatedFill CreateFill(SimulatedOrder order, long fillTimeUtc, decimal basePrice, bool isMaker)
    {
        var fillPrice = _feeModel.ApplySlippage(basePrice, order.Side);
        var fee = _feeModel.CalculateFee(order.Size, fillPrice, isMaker);

        return new SimulatedFill
        {
            OrderId = order.OrderId,
            FillTimeUtc = fillTimeUtc,
            FillPrice = fillPrice,
            Side = order.Side,
            Size = order.Size,
            Fee = fee,
            Symbol = order.Symbol,
            TradeType = order.TradeType,
            IsMaker = isMaker
        };
    }

    private void UpdatePosition(SimulatedFill fill)
    {
        _position.Symbol = fill.Symbol;
        _position.RealisedPnL -= fill.Fee;

        if (fill.Side == OrderSide.Buy)
        {
            UpdatePositionForBuy(fill);
            return;
        }

        UpdatePositionForSell(fill);
    }

    private void UpdatePositionForBuy(SimulatedFill fill)
    {
        if (_position.Size >= 0)
        {
            var newSize = _position.Size + fill.Size;
            var totalCost = (_position.AverageEntryPrice * _position.Size) + (fill.FillPrice * fill.Size);

            _position.Size = newSize;
            _position.AverageEntryPrice = newSize == 0 ? 0m : totalCost / newSize;
            return;
        }

        var existingShortSize = Math.Abs(_position.Size);
        var closedSize = Math.Min(fill.Size, existingShortSize);

        _position.RealisedPnL += (_position.AverageEntryPrice - fill.FillPrice) * closedSize;
        _position.Size += fill.Size;

        if (_position.Size > 0)
        {
            _position.AverageEntryPrice = fill.FillPrice;
            return;
        }

        if (_position.Size == 0)
        {
            _position.AverageEntryPrice = 0m;
        }
    }

    private void UpdatePositionForSell(SimulatedFill fill)
    {
        if (_position.Size <= 0)
        {
            var existingShortSize = Math.Abs(_position.Size);
            var newShortSize = existingShortSize + fill.Size;
            var totalProceeds = (_position.AverageEntryPrice * existingShortSize) + (fill.FillPrice * fill.Size);

            _position.Size = -newShortSize;
            _position.AverageEntryPrice = newShortSize == 0 ? 0m : totalProceeds / newShortSize;
            return;
        }

        var closedSize = Math.Min(fill.Size, _position.Size);

        _position.RealisedPnL += (fill.FillPrice - _position.AverageEntryPrice) * closedSize;
        _position.Size -= fill.Size;

        if (_position.Size < 0)
        {
            _position.AverageEntryPrice = fill.FillPrice;
            return;
        }

        if (_position.Size == 0)
        {
            _position.AverageEntryPrice = 0m;
        }
    }

    private void UpdateUnrealisedPnl(Candle candle)
    {
        if (!_position.IsOpen)
        {
            _position.UnrealisedPnL = 0m;
            return;
        }

        if (!string.Equals(_position.Symbol, candle.Symbol, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _position.UnrealisedPnL = _position.Size > 0
            ? (candle.Close - _position.AverageEntryPrice) * _position.Size
            : (_position.AverageEntryPrice - candle.Close) * Math.Abs(_position.Size);
    }
}