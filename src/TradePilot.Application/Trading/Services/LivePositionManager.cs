using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Trading.Services;

/// <summary>
/// Live <see cref="IPositionManager"/> that delegates order execution to the
/// <see cref="IExecutionEngine"/> interface. Unlike BacktestPositionManager this
/// implementation does not hard-cast to a specific engine type and has no backtest coupling.
/// Designed for the Worker service execution agent.
/// </summary>
public sealed class LivePositionManager : IPositionManager
{
    private readonly IExecutionEngine _executionEngine;
    private readonly IOrderTracker _orderTracker;
    private readonly IRiskEngine _riskEngine;
    private readonly ITriggerOrderManager? _triggerOrderManager;
    private readonly ILogger<LivePositionManager> _logger;

    private IGridCycleRepository? _gridCycleRepository;
    private ILiveOrderRepository? _orderRepository;
    private string _userId = string.Empty;
    private ProtectionOrderState? _protectionOrderState;

    public LivePositionManager(
        IExecutionEngine executionEngine,
        IOrderTracker orderTracker,
        IRiskEngine riskEngine,
        ILogger<LivePositionManager> logger,
        ITriggerOrderManager? triggerOrderManager = null)
    {
        _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
        _orderTracker = orderTracker ?? throw new ArgumentNullException(nameof(orderTracker));
        _riskEngine = riskEngine ?? throw new ArgumentNullException(nameof(riskEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _triggerOrderManager = triggerOrderManager;
    }

    /// <summary>
    /// Configures optional scoped repositories for DB persistence.
    /// Called from session setup where scoped services are available.
    /// </summary>
    public void ConfigureRepositories(
        IGridCycleRepository? gridCycleRepository,
        ILiveOrderRepository? orderRepository,
        string? userId)
    {
        _gridCycleRepository = gridCycleRepository;
        _orderRepository = orderRepository;
        _userId = userId ?? string.Empty;
    }

    /// <summary>
    /// Configures the protection order state so the position manager can cancel
    /// exchange-native TP/SL triggers before placing app-side exit orders.
    /// </summary>
    public void ConfigureProtectionState(ProtectionOrderState protectionOrderState)
    {
        _protectionOrderState = protectionOrderState;
    }

    public async Task ExecuteSignalsAsync(
        IReadOnlyList<TradingSignal> approvedSignals,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(approvedSignals);

        foreach (var signal in approvedSignals)
        {
            _logger.LogInformation(
                "Processing signal: Type={SignalType}, Symbol={Symbol}, Reason={Reason}",
                signal.SignalType, signal.Symbol, signal.Reason);

            switch (signal.SignalType)
            {
                case "DeployGrid":
                    await DeployGridAsync(signal, cancellationToken);
                    break;

                case "OpenPosition":
                    await OpenSignalPositionAsync(signal, cancellationToken);
                    break;

                case "TakeProfit":
                    await PlaceTakeProfitAsync(signal, cancellationToken);
                    break;

                case "CancelGrid":
                    // Cancel exchange-native protection triggers along with grid orders
                    if (_triggerOrderManager is not null && _protectionOrderState is not null
                        && _protectionOrderState.HasAny)
                    {
                        await _triggerOrderManager.CancelProtectionOrdersAsync(
                            _protectionOrderState, cancellationToken);
                    }

                    await _executionEngine.CancelAllOrdersAsync(signal.Symbol, cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Unknown signal type: {SignalType}", signal.SignalType);
                    break;
            }
        }
    }

    private async Task DeployGridAsync(TradingSignal signal, CancellationToken cancellationToken)
    {
        await _executionEngine.CancelAllOrdersAsync(signal.Symbol, cancellationToken);

        var leverage = GetOptionalInt(signal.Parameters, "leverage");
        if (leverage is > 0)
        {
            var isIsolated = GetOptionalBool(signal.Parameters, "isIsolated");
            await _executionEngine.SetLeverageAsync(signal.Symbol, leverage.Value, isIsolated, cancellationToken);
        }

        var anchorPrice = GetDecimal(signal.Parameters, "anchorPrice");
        var gridLevels = GetInt(signal.Parameters, "gridLevels");
        var gridSpacingPercent = Math.Abs(GetDecimal(signal.Parameters, "gridSpacingPercent"));
        var notionalPerLevel = Math.Abs(GetDecimal(signal.Parameters, "notionalUsd"));
        var gridCycleId = GetGridCycleId(signal.Parameters);
        var entryMode = GetOptionalString(signal.Parameters, "entryMode") ?? EntryModes.AutoFromSignalCandle;

        _logger.LogInformation(
            "Deploying grid: Symbol={Symbol}, AnchorPrice={AnchorPrice}, Levels={Levels}, Spacing={Spacing}%, Notional={Notional}, EntryMode={EntryMode}",
            signal.Symbol, anchorPrice, gridLevels, gridSpacingPercent, notionalPerLevel, entryMode);

        var placedOrders = new List<(string OrderId, int Level, decimal Price, decimal Size)>();
        var firstLimitLevel = 1;

        if (string.Equals(entryMode, EntryModes.InitialMarketThenGrid, StringComparison.Ordinal))
        {
            var marketSize = decimal.Round(notionalPerLevel / anchorPrice, 8, MidpointRounding.AwayFromZero);
            if (marketSize > 0m)
            {
                var orderId = await _executionEngine.PlaceOrderAsync(
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
                    cancellationToken);

                _orderTracker.TrackOrder(orderId, gridCycleId, 1, signal.Symbol,
                    OrderSide.Buy, anchorPrice, marketSize, TradeType.GridFill);

                placedOrders.Add((orderId, 1, anchorPrice, marketSize));
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

            var orderId = await _executionEngine.PlaceOrderAsync(
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
                cancellationToken);

            _orderTracker.TrackOrder(orderId, gridCycleId, level, signal.Symbol,
                OrderSide.Buy, price, size, TradeType.GridFill);

            placedOrders.Add((orderId, level, price, size));
        }

        _riskEngine.RecordOrdersPlaced(placedOrders.Count);

        await PersistGridDeploymentAsync(
            gridCycleId, signal.Symbol, anchorPrice, gridLevels, placedOrders, cancellationToken);
    }

    private async Task OpenSignalPositionAsync(TradingSignal signal, CancellationToken cancellationToken)
    {
        var entryPrice = GetDecimal(signal.Parameters, "entryPrice");
        var size = Math.Abs(GetDecimal(signal.Parameters, "size"));
        var tradeType = ResolveTradeType(signal.Parameters);
        var gridCycleId = GetOptionalString(signal.Parameters, "gridCycleId")
            ?? (tradeType == TradeType.DcaBuy ? "dca" : "signal");

        if (size <= 0m)
        {
            return;
        }

        var assetType = ResolveAssetType(signal, tradeType);
        var symbol = assetType == AssetType.Spot
            ? NormalizeSpotMarket(signal.Symbol)
            : signal.Symbol;

        var orderId = await _executionEngine.PlaceOrderAsync(
            new OrderRequest
            {
                Symbol = symbol,
                AssetType = assetType,
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Price = entryPrice,
                Size = size,
                TradeType = tradeType,
                GridCycleId = gridCycleId
            },
            cancellationToken);

        _orderTracker.TrackOrder(orderId, gridCycleId, 0, symbol,
            OrderSide.Buy, entryPrice, size, tradeType);
    }

    private async Task PlaceTakeProfitAsync(TradingSignal signal, CancellationToken cancellationToken)
    {
        // Cancel exchange-native protection orders before placing app-side exit
        // to prevent double-execution
        if (_triggerOrderManager is not null && _protectionOrderState is not null
            && _protectionOrderState.HasAny)
        {
            _logger.LogInformation(
                "Cancelling exchange-native protection orders before app-side exit: Symbol={Symbol}",
                signal.Symbol);
            await _triggerOrderManager.CancelProtectionOrdersAsync(
                _protectionOrderState, cancellationToken);
        }

        var orderType = Enum.Parse<OrderType>(GetString(signal.Parameters, "orderType"), ignoreCase: true);
        var gridCycleId = GetGridCycleId(signal.Parameters);

        await _executionEngine.CancelAllOrdersAsync(signal.Symbol, cancellationToken);

        var size = Math.Abs(GetDecimal(signal.Parameters, "size"));
        if (size <= 0m)
        {
            return;
        }

        var targetPrice = orderType == OrderType.Market
            ? 0m
            : GetDecimal(signal.Parameters, "targetPrice");

        var orderId = await _executionEngine.PlaceOrderAsync(
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

        _orderTracker.TrackOrder(orderId, gridCycleId, 0, signal.Symbol,
            OrderSide.Sell, targetPrice, size, TradeType.TakeProfit);
    }

    private async Task PersistGridDeploymentAsync(
        string gridCycleId,
        string symbol,
        decimal anchorPrice,
        int totalLevels,
        List<(string OrderId, int Level, decimal Price, decimal Size)> placedOrders,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_gridCycleRepository is not null && gridCycleId is not "signal" and not "default")
            {
                await _gridCycleRepository.AddAsync(new GridCycle
                {
                    Id = Guid.NewGuid(),
                    GridCycleId = gridCycleId,
                    StrategyName = string.Empty,
                    Symbol = symbol,
                    AnchorPrice = anchorPrice,
                    TotalLevels = totalLevels,
                    FilledLevels = 0,
                    Lifecycle = GridLifecycle.Active.ToString(),
                    StartedAtUtc = DateTime.UtcNow,
                    UserId = _userId,
                }, cancellationToken);
            }

            if (_orderRepository is not null)
            {
                foreach (var (orderId, level, price, size) in placedOrders)
                {
                    await _orderRepository.AddAsync(new LiveOrder
                    {
                        Id = Guid.NewGuid(),
                        OrderId = orderId,
                        GridCycleId = gridCycleId,
                        Level = level,
                        Symbol = symbol,
                        Side = OrderSide.Buy,
                        OrderType = level == 1 ? "Market" : "Limit",
                        Price = price,
                        Size = size,
                        TradeType = TradeType.GridFill.ToString(),
                        Status = OrderStatus.Pending,
                        PlacedAtUtc = DateTime.UtcNow,
                        UserId = _userId,
                    }, cancellationToken);
                }
            }

            _logger.LogInformation(
                "Grid deployment persisted: GridCycleId={GridCycleId}, Orders={OrderCount}",
                gridCycleId, placedOrders.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to persist grid deployment: GridCycleId={GridCycleId}. Trading continues.",
                gridCycleId);
        }
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

    private static TradeType ResolveTradeType(IReadOnlyDictionary<string, object>? parameters)
    {
        var rawTradeType = GetOptionalString(parameters, "tradeType");
        return Enum.TryParse<TradeType>(rawTradeType, ignoreCase: true, out var tradeType)
            ? tradeType
            : TradeType.SignalEntry;
    }

    private static AssetType ResolveAssetType(TradingSignal signal, TradeType tradeType)
    {
        var rawAssetType = GetOptionalString(signal.Parameters, "assetType");
        if (Enum.TryParse<AssetType>(rawAssetType, ignoreCase: true, out var assetType))
        {
            return assetType;
        }

        if (tradeType != TradeType.DcaBuy)
        {
            return AssetType.Perp;
        }

        return signal.Symbol.EndsWith("-PERP", StringComparison.OrdinalIgnoreCase)
            ? AssetType.Perp
            : AssetType.Spot;
    }

    private static int? GetOptionalInt(IReadOnlyDictionary<string, object>? parameters, string key)
    {
        if (parameters is null || !parameters.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => checked((int)longValue),
            decimal decimalValue => decimal.ToInt32(decimalValue),
            double doubleValue => Convert.ToInt32(doubleValue),
            string stringValue when int.TryParse(stringValue, out var parsedInt) => parsedInt,
            _ => Convert.ToInt32(value)
        };
    }

    private static bool GetOptionalBool(IReadOnlyDictionary<string, object>? parameters, string key)
    {
        if (parameters is null || !parameters.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            bool boolValue => boolValue,
            string stringValue when bool.TryParse(stringValue, out var parsedBool) => parsedBool,
            _ => Convert.ToBoolean(value)
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

    private static string NormalizeSpotMarket(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var trimmed = symbol.Trim();
        if (trimmed.EndsWith("-USD", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.ToUpperInvariant();
        }

        if (trimmed.EndsWith("-PERP", StringComparison.OrdinalIgnoreCase))
        {
            return $"{trimmed[..^5].Trim().ToUpperInvariant()}-USD";
        }

        return trimmed.Contains('-')
            ? trimmed.ToUpperInvariant()
            : $"{trimmed.ToUpperInvariant()}-USD";
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
