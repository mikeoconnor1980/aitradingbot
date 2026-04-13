using Microsoft.Extensions.Logging;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Trading.Services;

/// <summary>
/// Places, updates, and cancels exchange-native TP/SL trigger orders to protect open positions.
/// Best-effort: failures are logged but never block the trading pipeline.
/// </summary>
public sealed class TriggerOrderManager : ITriggerOrderManager
{
    private readonly IExecutionEngine _executionEngine;
    private readonly ILogger<TriggerOrderManager> _logger;

    public TriggerOrderManager(IExecutionEngine executionEngine, ILogger<TriggerOrderManager> logger)
    {
        _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PlaceProtectionOrdersAsync(
        PositionState positionState,
        ExitConfig exitConfig,
        MarketContext context,
        ProtectionOrderState protectionState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(positionState);
        ArgumentNullException.ThrowIfNull(exitConfig);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(protectionState);

        if (!positionState.IsOpen)
        {
            return;
        }

        var isLong = positionState.Size > 0;
        var absSize = Math.Abs(positionState.Size);

        // Place stop loss trigger
        if (exitConfig.StopLoss.Enabled && !protectionState.HasStopLoss)
        {
            var slPrice = CalculateStopLossPrice(positionState, exitConfig.StopLoss, context);
            if (slPrice.HasValue && slPrice.Value > 0)
            {
                await PlaceTriggerAsync(
                    context.Symbol, isLong ? "sell" : "buy", absSize, slPrice.Value, "sl",
                    protectionState, isStopLoss: true, cancellationToken);
            }
        }

        // Place take profit trigger
        if (exitConfig.TakeProfit.Enabled && !protectionState.HasTakeProfit)
        {
            var tpStopLossPercent = ResolveTakeProfitStopLossPercent(positionState, exitConfig, context);
            var tpPrice = CalculateTakeProfitPrice(positionState, exitConfig.TakeProfit, tpStopLossPercent);
            if (tpPrice.HasValue && tpPrice.Value > 0)
            {
                await PlaceTriggerAsync(
                    context.Symbol, isLong ? "sell" : "buy", absSize, tpPrice.Value, "tp",
                    protectionState, isStopLoss: false, cancellationToken);
            }
        }
    }

    public async Task UpdateProtectionOrdersAsync(
        PositionState positionState,
        ExitConfig exitConfig,
        MarketContext context,
        ProtectionOrderState protectionState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(positionState);
        ArgumentNullException.ThrowIfNull(exitConfig);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(protectionState);

        if (!positionState.IsOpen)
        {
            return;
        }

        var isLong = positionState.Size > 0;
        var absSize = Math.Abs(positionState.Size);

        // Update stop loss if price changed (e.g., trailing stop moved)
        if (exitConfig.StopLoss.Enabled && protectionState.HasStopLoss
            && exitConfig.StopLoss.Type != ExitRuleType.AtrInitial)
        {
            var newSlPrice = CalculateStopLossPrice(positionState, exitConfig.StopLoss, context);
            if (newSlPrice.HasValue && newSlPrice.Value > 0
                && newSlPrice.Value != protectionState.StopLossTriggerPrice)
            {
                await ModifyTriggerAsync(
                    protectionState.StopLossOrderId!, context.Symbol,
                    isLong ? "sell" : "buy", newSlPrice.Value, absSize, "sl",
                    protectionState, isStopLoss: true, cancellationToken);
            }
        }

        // Update take profit if price changed (e.g., average entry changed from new fill)
        if (exitConfig.TakeProfit.Enabled && protectionState.HasTakeProfit)
        {
                var tpStopLossPercent = ResolveTakeProfitStopLossPercent(positionState, exitConfig, context);
                var newTpPrice = CalculateTakeProfitPrice(positionState, exitConfig.TakeProfit, tpStopLossPercent);
            if (newTpPrice.HasValue && newTpPrice.Value > 0
                && newTpPrice.Value != protectionState.TakeProfitTriggerPrice)
            {
                await ModifyTriggerAsync(
                    protectionState.TakeProfitOrderId!, context.Symbol,
                    isLong ? "sell" : "buy", newTpPrice.Value, absSize, "tp",
                    protectionState, isStopLoss: false, cancellationToken);
            }
        }
    }

    public async Task CancelProtectionOrdersAsync(
        ProtectionOrderState protectionState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(protectionState);

        if (protectionState.HasStopLoss)
        {
            await CancelTriggerAsync(protectionState.StopLossOrderId!, cancellationToken);
        }

        if (protectionState.HasTakeProfit)
        {
            await CancelTriggerAsync(protectionState.TakeProfitOrderId!, cancellationToken);
        }

        protectionState.Clear();
    }

    internal static decimal? CalculateStopLossPrice(
        PositionState positionState,
        ExitRuleConfig stopLossConfig,
        MarketContext context)
    {
        if (!stopLossConfig.Enabled || !positionState.IsOpen)
        {
            return null;
        }

        var isLong = positionState.Size > 0;

        if (stopLossConfig.Type == ExitRuleType.AtrTrailing)
        {
            var atr = context.Indicators?.Atr ?? 0m;
            var multiplier = stopLossConfig.AtrMultiplier ?? 3m;

            if (atr <= 0m)
            {
                return null;
            }

            // For ATR trailing, use the current candle high as a proxy for high watermark
            // when called from initial placement. The GridController's own high watermark
            // tracking is more accurate; this is the initial backstop level.
            var referencePrice = context.CurrentCandle.High;
            return isLong
                ? referencePrice - (atr * multiplier)
                : referencePrice + (atr * multiplier);
        }

        if (stopLossConfig.Type == ExitRuleType.AtrInitial)
        {
            var atr = context.Indicators?.Atr ?? 0m;
            var multiplier = stopLossConfig.AtrMultiplier ?? 2m;

            if (atr <= 0m)
            {
                if (stopLossConfig.Value.HasValue)
                {
                    var percent = Math.Abs(stopLossConfig.Value.Value);
                    return isLong
                        ? positionState.AverageEntryPrice * (1m - (percent / 100m))
                        : positionState.AverageEntryPrice * (1m + (percent / 100m));
                }

                return null;
            }

            var entryPrice = positionState.AverageEntryPrice;
            return isLong
                ? entryPrice - (atr * multiplier)
                : entryPrice + (atr * multiplier);
        }

        if (stopLossConfig.Value.HasValue)
        {
            var percent = Math.Abs(stopLossConfig.Value.Value);
            return isLong
                ? positionState.AverageEntryPrice * (1m - (percent / 100m))
                : positionState.AverageEntryPrice * (1m + (percent / 100m));
        }

        return null;
    }

    internal static decimal? CalculateTakeProfitPrice(
        PositionState positionState,
        ExitRuleConfig takeProfitConfig,
        decimal? stopLossPercent = null)
    {
        if (!takeProfitConfig.Enabled || !takeProfitConfig.Value.HasValue || !positionState.IsOpen)
        {
            return null;
        }

        var isLong = positionState.Size > 0;

        if (takeProfitConfig.Type == ExitRuleType.RMultiple)
        {
            if (!stopLossPercent.HasValue || stopLossPercent.Value <= 0m)
            {
                return null;
            }

            var rMultiple = Math.Abs(takeProfitConfig.Value.Value);
            var effectivePercent = stopLossPercent.Value * rMultiple;

            return isLong
                ? positionState.AverageEntryPrice * (1m + (effectivePercent / 100m))
                : positionState.AverageEntryPrice * (1m - (effectivePercent / 100m));
        }

        var percent = Math.Abs(takeProfitConfig.Value.Value);

        return isLong
            ? positionState.AverageEntryPrice * (1m + (percent / 100m))
            : positionState.AverageEntryPrice * (1m - (percent / 100m));
    }

    private static decimal? ResolveTakeProfitStopLossPercent(
        PositionState positionState,
        ExitConfig exitConfig,
        MarketContext context)
    {
        if (exitConfig.TakeProfit.Type != ExitRuleType.RMultiple)
        {
            return null;
        }

        return StopLossDistanceResolver.Resolve(
            exitConfig.StopLoss,
            context.Indicators?.Atr,
            positionState.AverageEntryPrice);
    }

    private async Task PlaceTriggerAsync(
        string symbol, string side, decimal size, decimal triggerPrice, string tpslType,
        ProtectionOrderState protectionState, bool isStopLoss,
        CancellationToken cancellationToken)
    {
        var label = isStopLoss ? "SL" : "TP";

        try
        {
            _logger.LogInformation(
                "Placing exchange-native {Label} trigger: Symbol={Symbol}, Side={Side}, Size={Size}, TriggerPrice={TriggerPrice}",
                label, symbol, side, size, triggerPrice);

            var orderId = await _executionEngine.PlaceTriggerOrderAsync(
                symbol, side, size, triggerPrice, tpslType, cancellationToken);

            if (string.IsNullOrEmpty(orderId))
            {
                _logger.LogWarning(
                    "Exchange rejected {Label} trigger order: Symbol={Symbol}, TriggerPrice={TriggerPrice}",
                    label, symbol, triggerPrice);
                return;
            }

            if (isStopLoss)
            {
                protectionState.StopLossOrderId = orderId;
                protectionState.StopLossTriggerPrice = triggerPrice;
            }
            else
            {
                protectionState.TakeProfitOrderId = orderId;
                protectionState.TakeProfitTriggerPrice = triggerPrice;
            }

            protectionState.LastUpdatedAtUtc = DateTime.UtcNow;

            _logger.LogInformation(
                "Exchange-native {Label} trigger placed: OrderId={OrderId}, TriggerPrice={TriggerPrice}",
                label, orderId, triggerPrice);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to place exchange-native {Label} trigger: Symbol={Symbol}, TriggerPrice={TriggerPrice}. App-side protection continues.",
                label, symbol, triggerPrice);
        }
    }

    private async Task ModifyTriggerAsync(
        string orderId, string symbol, string side, decimal triggerPrice, decimal size, string tpslType,
        ProtectionOrderState protectionState, bool isStopLoss,
        CancellationToken cancellationToken)
    {
        var label = isStopLoss ? "SL" : "TP";

        try
        {
            _logger.LogInformation(
                "Updating exchange-native {Label} trigger: OrderId={OrderId}, NewPrice={TriggerPrice}",
                label, orderId, triggerPrice);

            await _executionEngine.ModifyTriggerOrderAsync(
                orderId, symbol, side, triggerPrice, size, tpslType, cancellationToken);

            if (isStopLoss)
            {
                protectionState.StopLossTriggerPrice = triggerPrice;
            }
            else
            {
                protectionState.TakeProfitTriggerPrice = triggerPrice;
            }

            protectionState.LastUpdatedAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to update exchange-native {Label} trigger: OrderId={OrderId}. App-side protection continues.",
                label, orderId);
        }
    }

    private async Task CancelTriggerAsync(string orderId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Cancelling protection trigger: OrderId={OrderId}", orderId);
            await _executionEngine.CancelOrderAsync(orderId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to cancel protection trigger: OrderId={OrderId}. May have already fired.",
                orderId);
        }
    }
}
