using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// In-memory execution engine used by backtests.
/// </summary>
public sealed class SimulatedExecutionEngine : IExecutionEngine
{
    private readonly FeeModel _feeModel;
    private readonly Dictionary<string, (int Leverage, bool IsIsolated)> _leverageByAsset = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _maxLeverageByAsset = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PositionLeverageContext> _leverageContext = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SimulatedOrder> _openOrders = [];
    private readonly List<SimulatedFill> _allFills = [];
    private readonly SimulatedPosition _position = new();
    private int _orderCounter;
    private long _currentTimestampUtc;
    private string? _currentGridCycleId;

    private sealed record PositionLeverageContext(
        decimal EntryPrice,
        int Leverage,
        decimal MaintenanceMarginRate,
        decimal LiquidationPrice,
        string Side,
        decimal MarginUsed);

    public SimulatedExecutionEngine(FeeModel feeModel)
    {
        _feeModel = feeModel ?? throw new ArgumentNullException(nameof(feeModel));
    }

    public IReadOnlyDictionary<string, (int Leverage, bool IsIsolated)> LeverageByAsset => _leverageByAsset;

    public IReadOnlyDictionary<string, int> MaxLeverageByAsset => _maxLeverageByAsset;

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
            AnchorPrice = order.AnchorPrice,
            Size = order.Size,
            TradeType = order.TradeType,
            GridCycleId = order.GridCycleId,
            CloseReason = order.CloseReason,
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

    public Task<string> PlaceTriggerOrderAsync(string asset, string side, decimal size, decimal triggerPrice, string tpslType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(side);
        ArgumentException.ThrowIfNullOrWhiteSpace(tpslType);

        var orderId = $"SIM-TRIG-{++_orderCounter:D6}";
        var orderSide = Enum.Parse<OrderSide>(side, ignoreCase: true);
        var closeReason = string.Equals(tpslType, "sl", StringComparison.OrdinalIgnoreCase)
            ? CancellationReason.StopLossTriggered
            : CancellationReason.TakeProfitTriggered;

        _openOrders.Add(new SimulatedOrder
        {
            OrderId = orderId,
            Symbol = asset,
            Side = orderSide,
            OrderType = OrderType.Market,
            Price = triggerPrice,
            TriggerPrice = triggerPrice,
            TriggerType = tpslType,
            Size = size,
            TradeType = TradeType.TakeProfit,
            GridCycleId = _currentGridCycleId,
            CloseReason = closeReason,
            PlacedAtUtc = _currentTimestampUtc
        });

        return Task.FromResult(orderId);
    }

    public Task ModifyTriggerOrderAsync(string orderId, string asset, string side, decimal triggerPrice, decimal size, string tpslType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(side);
        ArgumentException.ThrowIfNullOrWhiteSpace(tpslType);

        var orderIndex = _openOrders.FindIndex(order => string.Equals(order.OrderId, orderId, StringComparison.Ordinal));
        if (orderIndex < 0)
        {
            return Task.CompletedTask;
        }

        var existingOrder = _openOrders[orderIndex];
        _openOrders[orderIndex] = new SimulatedOrder
        {
            OrderId = existingOrder.OrderId,
            Symbol = asset,
            Side = Enum.Parse<OrderSide>(side, ignoreCase: true),
            OrderType = OrderType.Market,
            Price = triggerPrice,
            TriggerPrice = triggerPrice,
            TriggerType = tpslType,
            Size = size,
            TradeType = existingOrder.TradeType,
            GridCycleId = existingOrder.GridCycleId,
            CloseReason = string.Equals(tpslType, "sl", StringComparison.OrdinalIgnoreCase)
                ? CancellationReason.StopLossTriggered
                : CancellationReason.TakeProfitTriggered,
            PlacedAtUtc = existingOrder.PlacedAtUtc
        };

        return Task.CompletedTask;
    }

    public Task SetLeverageAsync(string asset, int leverage, bool isIsolated, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(asset);

        _leverageByAsset[asset] = (leverage, isIsolated);
        return Task.CompletedTask;
    }

    public void SetMaxLeverage(string asset, int maxLeverage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(asset);
        _maxLeverageByAsset[asset] = maxLeverage > 0 ? maxLeverage : LeverageCalculator.FallbackMaxLeverage;
    }

    public IReadOnlyList<SimulatedFill> ProcessCandle(Candle candle)
    {
        ArgumentNullException.ThrowIfNull(candle);

        _currentTimestampUtc = candle.Timestamp;
        var candleFills = new List<SimulatedFill>();
        var filledOrderIds = new HashSet<string>(StringComparer.Ordinal);

        var protectionFill = TryProcessProtectionOrLiquidation(candle);
        if (protectionFill is not null)
        {
            candleFills.Add(protectionFill);
            _allFills.Add(protectionFill);
            UpdateUnrealisedPnl(candle);
            return candleFills;
        }

        var orderedOrders = _openOrders
            .Where(order => !order.TriggerPrice.HasValue)
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

    public IReadOnlyList<SimulatedOrder> GetOpenOrders() => _openOrders.ToList();

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
            GridCycleId = order.GridCycleId,
            CloseReason = order.CloseReason,
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
            RefreshPositionContext(fill.Symbol, fill.GridCycleId);
            return;
        }

        UpdatePositionForSell(fill);
        RefreshPositionContext(fill.Symbol, fill.GridCycleId);
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

    private SimulatedFill? TryProcessProtectionOrLiquidation(Candle candle)
    {
        if (!_position.IsOpen || !string.Equals(_position.Symbol, candle.Symbol, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var stopLossOrder = _openOrders.FirstOrDefault(order =>
            string.Equals(order.Symbol, candle.Symbol, StringComparison.OrdinalIgnoreCase)
            && order.TriggerPrice.HasValue
            && order.CloseReason == CancellationReason.StopLossTriggered);

        if (stopLossOrder is not null && IsTriggerBreached(stopLossOrder, candle))
        {
            var liquidationBreached = IsLiquidationBreached(candle.Symbol, candle.Low, candle.High);
            var stopLossGapped = IsGapBeyondTrigger(stopLossOrder, candle);

            if (!liquidationBreached || !stopLossGapped)
            {
                return ExecuteProtectionOrder(stopLossOrder, candle);
            }
        }

        var takeProfitTriggerOrder = _openOrders.FirstOrDefault(order =>
            string.Equals(order.Symbol, candle.Symbol, StringComparison.OrdinalIgnoreCase)
            && order.TriggerPrice.HasValue
            && order.CloseReason == CancellationReason.TakeProfitTriggered);

        if (takeProfitTriggerOrder is not null && IsTriggerBreached(takeProfitTriggerOrder, candle))
        {
            return ExecuteProtectionOrder(takeProfitTriggerOrder, candle);
        }

        if (TryCreateLiquidationFill(candle.Symbol, candle.Low, candle.High, out var liquidationFill))
        {
            return liquidationFill;
        }

        return null;
    }

    private SimulatedFill ExecuteProtectionOrder(SimulatedOrder order, Candle candle)
    {
        var triggerPrice = order.TriggerPrice ?? order.Price;
        var fillPrice = _feeModel.ApplySlippage(triggerPrice, order.Side);
        var fee = _feeModel.CalculateFee(order.Size, fillPrice, isMaker: false);
        var fill = new SimulatedFill
        {
            OrderId = order.OrderId,
            FillTimeUtc = candle.Timestamp,
            FillPrice = fillPrice,
            Side = order.Side,
            Size = order.Size,
            Fee = fee,
            Symbol = order.Symbol,
            TradeType = order.TradeType,
            GridCycleId = order.GridCycleId ?? _currentGridCycleId,
            CloseReason = order.CloseReason,
            IsMaker = false
        };

        UpdatePosition(fill);
        RemoveOrdersForSymbol(order.Symbol);
        return fill;
    }

    private bool TryCreateLiquidationFill(string asset, decimal candleLow, decimal candleHigh, out SimulatedFill fill)
    {
        fill = default!;

        if (!_leverageContext.TryGetValue(asset, out var context) || context.Leverage <= 1)
        {
            return false;
        }

        var breached = context.Side == "buy"
            ? candleLow <= context.LiquidationPrice
            : candleHigh >= context.LiquidationPrice;

        if (!breached)
        {
            return false;
        }

        var side = _position.Size > 0m ? OrderSide.Sell : OrderSide.Buy;
        var size = Math.Abs(_position.Size);
        var fee = _feeModel.CalculateFee(size, context.LiquidationPrice, isMaker: false);

        fill = new SimulatedFill
        {
            OrderId = $"SIM-LIQ-{++_orderCounter:D6}",
            FillTimeUtc = _currentTimestampUtc,
            FillPrice = context.LiquidationPrice,
            Side = side,
            Size = size,
            Fee = fee,
            Symbol = asset,
            TradeType = TradeType.TakeProfit,
            GridCycleId = _currentGridCycleId,
            CloseReason = CancellationReason.LiquidationTriggered,
            IsMaker = false
        };

        UpdatePosition(fill);
        RemoveOrdersForSymbol(asset);
        return true;
    }

    private bool IsLiquidationBreached(string asset, decimal candleLow, decimal candleHigh)
    {
        if (!_leverageContext.TryGetValue(asset, out var context) || context.Leverage <= 1)
        {
            return false;
        }

        return context.Side == "buy"
            ? candleLow <= context.LiquidationPrice
            : candleHigh >= context.LiquidationPrice;
    }

    private static bool IsTriggerBreached(SimulatedOrder order, Candle candle)
    {
        if (!order.TriggerPrice.HasValue)
        {
            return false;
        }

        return order.Side == OrderSide.Sell
            ? candle.Low <= order.TriggerPrice.Value
            : candle.High >= order.TriggerPrice.Value;
    }

    private static bool IsGapBeyondTrigger(SimulatedOrder order, Candle candle)
    {
        if (!order.TriggerPrice.HasValue)
        {
            return false;
        }

        return order.Side == OrderSide.Sell
            ? candle.Open <= order.TriggerPrice.Value
            : candle.Open >= order.TriggerPrice.Value;
    }

    private void RefreshPositionContext(string symbol, string? gridCycleId)
    {
        if (!_position.IsOpen)
        {
            ClearPositionContext(symbol);
            return;
        }

        if (!string.IsNullOrWhiteSpace(gridCycleId))
        {
            _currentGridCycleId = gridCycleId;
        }

        var leverageInfo = _leverageByAsset.TryGetValue(symbol, out var configuredLeverage)
            ? configuredLeverage
            : (Leverage: 1, IsIsolated: false);
        var leverage = leverageInfo.Leverage > 0 ? leverageInfo.Leverage : 1;
        var maxLeverage = _maxLeverageByAsset.TryGetValue(symbol, out var configuredMaxLeverage)
            ? configuredMaxLeverage
            : LeverageCalculator.FallbackMaxLeverage;
        var maintenanceMarginRate = LeverageCalculator.DeriveMaintenanceMarginRate(maxLeverage);
        var liquidationPrice = _position.Size > 0m
            ? _position.AverageEntryPrice * (1m - (1m / leverage) + maintenanceMarginRate)
            : _position.AverageEntryPrice * (1m + (1m / leverage) - maintenanceMarginRate);
        var marginUsed = decimal.Round((_position.AverageEntryPrice * Math.Abs(_position.Size)) / leverage, 8, MidpointRounding.AwayFromZero);
        var side = _position.Size > 0m ? "buy" : "sell";

        _leverageContext[symbol] = new PositionLeverageContext(
            _position.AverageEntryPrice,
            leverage,
            maintenanceMarginRate,
            liquidationPrice,
            side,
            marginUsed);

        _position.Leverage = leverage;
        _position.MarginUsed = marginUsed;
        _position.LiquidationPrice = leverage > 1 ? liquidationPrice : 0m;
    }

    private void ClearPositionContext(string symbol)
    {
        _leverageContext.Remove(symbol);
        _currentGridCycleId = null;
        _position.Leverage = 0;
        _position.MarginUsed = 0m;
        _position.LiquidationPrice = 0m;
    }

    private void RemoveOrdersForSymbol(string symbol)
    {
        _openOrders.RemoveAll(order => string.Equals(order.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
    }
}