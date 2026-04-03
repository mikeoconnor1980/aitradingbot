using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Enums;
using TradingApp.Domain.Trading;

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

                case "OpenPosition":
                    await OpenSignalPositionAsync(executionEngine, signal, cancellationToken);
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
        var entryMode = GetOptionalString(signal.Parameters, "entryMode") ?? EntryModes.AutoFromSignalCandle;

        var firstLimitLevel = 1;

        if (string.Equals(entryMode, EntryModes.InitialMarketThenGrid, StringComparison.Ordinal))
        {
            var marketSize = decimal.Round(notionalPerLevel / anchorPrice, 8, MidpointRounding.AwayFromZero);
            if (marketSize > 0m)
            {
                await PlaceAndLogOrderAsync(
                    executionEngine,
                    new OrderRequest
                    {
                        Symbol = signal.Symbol,
                        Side = OrderSide.Buy,
                        OrderType = OrderType.Market,
                        Price = anchorPrice,
                        AnchorPrice = anchorPrice,
                        Size = marketSize,
                        TradeType = TradeType.GridFill,
                        GridCycleId = gridCycleId
                    },
                    gridCycleId,
                    cancellationToken);
            }

            firstLimitLevel = 2;
        }

        for (var level = firstLimitLevel; level <= gridLevels; level++)
        {
            var ladderOffset = string.Equals(entryMode, EntryModes.InitialMarketThenGrid, StringComparison.Ordinal)
                ? level - 1
                : level;
            var price = anchorPrice * (1m - ((gridSpacingPercent / 100m) * ladderOffset));
            if (price <= 0m)
            {
                continue;
            }

            var size = decimal.Round(notionalPerLevel / price, 8, MidpointRounding.AwayFromZero);
            if (size <= 0m)
            {
                continue;
            }

            await PlaceAndLogOrderAsync(
                executionEngine,
                new OrderRequest
                {
                    Symbol = signal.Symbol,
                    Side = OrderSide.Buy,
                    OrderType = OrderType.Limit,
                    Price = price,
                    AnchorPrice = anchorPrice,
                    Size = size,
                    TradeType = TradeType.GridFill,
                    GridCycleId = gridCycleId
                },
                gridCycleId,
                cancellationToken);
        }
    }

    private async Task OpenSignalPositionAsync(
        Backtesting.Services.SimulatedExecutionEngine executionEngine,
        TradingSignal signal,
        CancellationToken cancellationToken)
    {
        var entryPrice = GetDecimal(signal.Parameters, "entryPrice");
        var size = Math.Abs(GetDecimal(signal.Parameters, "size"));

        if (size <= 0m)
        {
            return;
        }

        await PlaceAndLogOrderAsync(
            executionEngine,
            new OrderRequest
            {
                Symbol = signal.Symbol,
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Price = entryPrice,
                Size = size,
                TradeType = TradeType.SignalEntry,
                GridCycleId = "signal"
            },
            "signal",
            cancellationToken);
    }

    private async Task PlaceTakeProfitAsync(
        Backtesting.Services.SimulatedExecutionEngine executionEngine,
        TradingSignal signal,
        CancellationToken cancellationToken)
    {
        var orderType = Enum.Parse<OrderType>(GetString(signal.Parameters, "orderType"), ignoreCase: true);
        var cancellationReason = TryGetCancellationReason(signal.Parameters) ??
            (orderType == OrderType.Market
                ? CancellationReason.StopLossTriggered
                : CancellationReason.TakeProfitTriggered);
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

        await PlaceAndLogOrderAsync(
            executionEngine,
            new OrderRequest
            {
                Symbol = signal.Symbol,
                Side = OrderSide.Sell,
                OrderType = orderType,
                Price = targetPrice,
                Size = size,
                TradeType = TradeType.TakeProfit,
                GridCycleId = gridCycleId,
                CloseReason = cancellationReason
            },
            gridCycleId,
            cancellationToken);
    }

    private async Task PlaceAndLogOrderAsync(
        Backtesting.Services.SimulatedExecutionEngine executionEngine,
        OrderRequest orderRequest,
        string gridCycleId,
        CancellationToken cancellationToken)
    {
        var orderId = await executionEngine.PlaceOrderAsync(orderRequest, cancellationToken);

        _auditCollector.LogOrderEvent(new OrderEventEntry
        {
            TimestampUtc = _executionContextAccessor.CurrentTimestampUtc,
            EventType = OrderEventType.Placed,
            OrderId = orderId,
            Side = orderRequest.Side.ToString(),
            OrderType = orderRequest.OrderType.ToString(),
            Price = orderRequest.Price,
            Size = orderRequest.Size,
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

    private static string? GetOptionalString(IReadOnlyDictionary<string, object>? parameters, string key)
    {
        if (parameters is null || !parameters.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value.ToString();
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

    private static CancellationReason? TryGetCancellationReason(IReadOnlyDictionary<string, object>? parameters)
    {
        var value = GetOptionalString(parameters, "cancellationReason");
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.Parse<CancellationReason>(value, ignoreCase: true);
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