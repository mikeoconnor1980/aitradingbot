using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Trading.Services;

public sealed class BacktestPositionManager : IPositionManager
{
    private readonly BacktestExecutionContextAccessor _executionContextAccessor;
    private IBacktestAuditCollector _auditCollector;

    public BacktestPositionManager(
        BacktestExecutionContextAccessor executionContextAccessor,
        IBacktestAuditCollector? auditCollector = null)
    {
        _executionContextAccessor = executionContextAccessor ?? throw new ArgumentNullException(nameof(executionContextAccessor));
        _auditCollector = auditCollector ?? NullBacktestAuditCollector.Instance;
    }

    public void SetAuditCollector(IBacktestAuditCollector collector)
    {
        _auditCollector = collector ?? NullBacktestAuditCollector.Instance;
    }

    public async Task ExecuteSignalsAsync(
        IReadOnlyList<TradingSignal> approvedSignals,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(approvedSignals);

        var executionEngine = _executionContextAccessor.CurrentExecutionEngine
            ?? throw new InvalidOperationException("Backtest execution engine is not available for the current run.");

        foreach (var signal in approvedSignals)
        {
            switch (signal.SignalType)
            {
                case "DeployGrid":
                    await DeployGridAsync(executionEngine, signal, cancellationToken);
                    break;

                case "TakeProfit":
                    await PlaceTakeProfitAsync(executionEngine, signal, cancellationToken);
                    break;

                case "CancelGrid":
                    await CancelOpenOrdersAsync(
                        executionEngine,
                        signal.Symbol,
                        CancellationReason.ManualCancel,
                        GetGridCycleId(signal.Parameters),
                        cancellationToken);
                    break;
            }
        }
    }

    private async Task DeployGridAsync(
        Backtesting.Services.SimulatedExecutionEngine executionEngine,
        TradingSignal signal,
        CancellationToken cancellationToken)
    {
        await CancelOpenOrdersAsync(
            executionEngine,
            signal.Symbol,
            CancellationReason.GridRedeployed,
            null,
            cancellationToken);

        var anchorPrice = GetDecimal(signal.Parameters, "anchorPrice");
        var gridLevels = GetInt(signal.Parameters, "gridLevels");
        var gridSpacingPercent = Math.Abs(GetDecimal(signal.Parameters, "gridSpacingPercent"));
        var notionalPerLevel = Math.Abs(GetDecimal(signal.Parameters, "notionalPerLevel"));
        var gridCycleId = GetGridCycleId(signal.Parameters);

        for (var level = 1; level <= gridLevels; level++)
        {
            var price = anchorPrice * (1m - ((gridSpacingPercent / 100m) * level));
            if (price <= 0m)
            {
                continue;
            }

            var size = decimal.Round(notionalPerLevel / price, 8, MidpointRounding.AwayFromZero);
            if (size <= 0m)
            {
                continue;
            }

            var orderId = await executionEngine.PlaceOrderAsync(
                new OrderRequest
                {
                    Symbol = signal.Symbol,
                    Side = OrderSide.Buy,
                    OrderType = OrderType.Limit,
                    Price = price,
                    Size = size,
                    TradeType = TradeType.GridFill,
                    GridCycleId = gridCycleId
                },
                cancellationToken);

            _auditCollector.LogOrderEvent(new OrderEventEntry
            {
                TimestampUtc = _executionContextAccessor.CurrentTimestampUtc,
                EventType = OrderEventType.Placed,
                OrderId = orderId,
                Side = OrderSide.Buy.ToString(),
                OrderType = OrderType.Limit.ToString(),
                Price = price,
                Size = size,
                GridCycleId = gridCycleId
            });
        }
    }

    private async Task PlaceTakeProfitAsync(
        Backtesting.Services.SimulatedExecutionEngine executionEngine,
        TradingSignal signal,
        CancellationToken cancellationToken)
    {
        var orderType = Enum.Parse<OrderType>(GetString(signal.Parameters, "orderType"), ignoreCase: true);
        var cancellationReason = orderType == OrderType.Market
            ? CancellationReason.StopLossTriggered
            : CancellationReason.PositionOpened;
        var gridCycleId = GetGridCycleId(signal.Parameters);

        await CancelOpenOrdersAsync(
            executionEngine,
            signal.Symbol,
            cancellationReason,
            gridCycleId,
            cancellationToken);

        var size = Math.Abs(GetDecimal(signal.Parameters, "size"));
        if (size <= 0m)
        {
            return;
        }
        var targetPrice = orderType == OrderType.Market
            ? 0m
            : GetDecimal(signal.Parameters, "targetPrice");

        var orderId = await executionEngine.PlaceOrderAsync(
            new OrderRequest
            {
                Symbol = signal.Symbol,
                Side = OrderSide.Sell,
                OrderType = orderType,
                Price = targetPrice,
                Size = size,
                TradeType = TradeType.TakeProfit,
                GridCycleId = gridCycleId
            },
            cancellationToken);

        _auditCollector.LogOrderEvent(new OrderEventEntry
        {
            TimestampUtc = _executionContextAccessor.CurrentTimestampUtc,
            EventType = OrderEventType.Placed,
            OrderId = orderId,
            Side = OrderSide.Sell.ToString(),
            OrderType = orderType.ToString(),
            Price = targetPrice,
            Size = size,
            GridCycleId = gridCycleId
        });
    }

    private async Task CancelOpenOrdersAsync(
        Backtesting.Services.SimulatedExecutionEngine executionEngine,
        string symbol,
        CancellationReason cancellationReason,
        string? fallbackGridCycleId,
        CancellationToken cancellationToken)
    {
        var openOrders = executionEngine.GetOpenOrders()
            .Where(order => string.Equals(order.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var order in openOrders)
        {
            _auditCollector.LogOrderEvent(new OrderEventEntry
            {
                TimestampUtc = _executionContextAccessor.CurrentTimestampUtc,
                EventType = OrderEventType.Cancelled,
                OrderId = order.OrderId,
                Side = order.Side.ToString(),
                OrderType = order.OrderType.ToString(),
                Price = order.Price,
                Size = order.Size,
                CancellationReason = cancellationReason,
                GridCycleId = order.GridCycleId ?? fallbackGridCycleId ?? "default"
            });
        }

        await executionEngine.CancelAllOrdersAsync(symbol, cancellationToken);
    }

    private static decimal GetDecimal(IReadOnlyDictionary<string, object>? parameters, string key)
    {
        var value = GetRequiredValue(parameters, key);
        return value switch
        {
            decimal decimalValue => decimalValue,
            double doubleValue => Convert.ToDecimal(doubleValue),
            float floatValue => Convert.ToDecimal(floatValue),
            int intValue => intValue,
            long longValue => longValue,
            string stringValue => decimal.Parse(stringValue),
            _ => Convert.ToDecimal(value)
        };
    }

    private static int GetInt(IReadOnlyDictionary<string, object>? parameters, string key)
    {
        var value = GetRequiredValue(parameters, key);
        return value switch
        {
            int intValue => intValue,
            long longValue => checked((int)longValue),
            decimal decimalValue => decimal.ToInt32(decimalValue),
            double doubleValue => Convert.ToInt32(doubleValue),
            string stringValue => int.Parse(stringValue),
            _ => Convert.ToInt32(value)
        };
    }

    private static string GetString(IReadOnlyDictionary<string, object>? parameters, string key)
    {
        var value = GetRequiredValue(parameters, key);
        return value.ToString()
            ?? throw new InvalidOperationException($"Signal parameter '{key}' could not be converted to a string.");
    }

    private static string GetGridCycleId(IReadOnlyDictionary<string, object>? parameters)
    {
        if (parameters is not null && parameters.TryGetValue("gridCycleId", out var value) && value is not null)
        {
            var gridCycleId = value.ToString();
            if (!string.IsNullOrWhiteSpace(gridCycleId))
            {
                return gridCycleId;
            }
        }

        return "default";
    }

    private static object GetRequiredValue(IReadOnlyDictionary<string, object>? parameters, string key)
    {
        if (parameters is null || !parameters.TryGetValue(key, out var value))
        {
            throw new InvalidOperationException($"Signal parameter '{key}' is required.");
        }

        return value;
    }
}