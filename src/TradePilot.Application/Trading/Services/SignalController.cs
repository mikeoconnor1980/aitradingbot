using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Trading.Services;

/// <summary>
/// Processes signal-mode strategy evaluation and emits position entry and exit signals.
/// </summary>
public sealed class SignalController : ISignalController
{
    public Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluationResult evaluation,
        MarketContext context,
        GridState gridState,
        PositionState positionState,
        IStrategyConfig strategyConfig,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(gridState);
        ArgumentNullException.ThrowIfNull(positionState);
        ArgumentNullException.ThrowIfNull(strategyConfig);

        if (strategyConfig is not StrategyConfig config)
        {
            throw new ArgumentException(
                $"Expected {nameof(StrategyConfig)} but received {strategyConfig.GetType().Name}.",
                nameof(strategyConfig));
        }

        if (positionState.IsOpen)
        {
            return Task.FromResult(EvaluateExitConditions(context, gridState, positionState, config));
        }

        if (!evaluation.SetupDetected)
        {
            return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
        }

        return Task.FromResult(EmitOpenPosition(context, gridState, config, evaluation.Reason));
    }

    private static IReadOnlyList<TradingSignal> EmitOpenPosition(
        MarketContext context,
        GridState gridState,
        StrategyConfig config,
        string? reason)
    {
        gridState.AtrAtEntry = config.Exit.StopLoss.Type == ExitRuleType.AtrInitial
            ? context.Indicators?.Atr
            : null;

        var entryPrice = context.CurrentCandle.Close;
        var stopLossPercent = config.Risk.PositionSizeType == PositionSizeType.RiskBased
            ? StopLossDistanceResolver.Resolve(
                config.Exit.StopLoss,
                context.Indicators?.Atr,
                entryPrice)
            : null;
        var notional = PositionSizeResolver.ResolveNotional(config.Risk, context.AccountEquity, stopLossPercent);
        notional *= context.DrawdownScalingFactor;
        var size = entryPrice > 0m
            ? decimal.Round(notional / entryPrice, 8, MidpointRounding.AwayFromZero)
            : 0m;

        if (size <= 0m)
        {
            return Array.Empty<TradingSignal>();
        }

        return
        [
            new TradingSignal
            {
                SignalType = "OpenPosition",
                Symbol = context.Symbol,
                Reason = reason ?? "Signal condition met.",
                Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["entryPrice"] = entryPrice,
                    ["size"] = size,
                    ["notionalUsd"] = notional,
                    ["orderType"] = OrderType.Market.ToString(),
                    ["gridCycleId"] = "signal"
                }
            }
        ];
    }

    private static IReadOnlyList<TradingSignal> EvaluateExitConditions(
        MarketContext context,
        GridState gridState,
        PositionState positionState,
        StrategyConfig config)
    {
        var stopLossConfig = config.Exit.StopLoss;
        var isAtrTrailing = stopLossConfig.Enabled && stopLossConfig.Type == ExitRuleType.AtrTrailing;
        var isAtrInitial = stopLossConfig.Enabled
            && stopLossConfig.Type == ExitRuleType.AtrInitial
            && gridState.AtrAtEntry.HasValue
            && gridState.AtrAtEntry.Value > 0m;
        var isFixedStopLoss = stopLossConfig.Enabled
            && (stopLossConfig.Type != ExitRuleType.AtrTrailing
                && stopLossConfig.Type != ExitRuleType.AtrInitial
                || (stopLossConfig.Type == ExitRuleType.AtrInitial && !isAtrInitial))
            && stopLossConfig.Value.HasValue;

        // ATR trailing stop
        if (isAtrTrailing)
        {
            gridState.CandlesSinceEntry++;

            var candleHigh = context.CurrentCandle.High;
            gridState.TrailingStopHighWatermark = gridState.TrailingStopHighWatermark.HasValue
                ? Math.Max(gridState.TrailingStopHighWatermark.Value, candleHigh)
                : candleHigh;

            var warmup = stopLossConfig.TrailingStopWarmup ?? 0;
            if (gridState.CandlesSinceEntry <= warmup)
            {
                return Array.Empty<TradingSignal>();
            }

            var atr = context.Indicators?.Atr ?? 0m;
            var multiplier = stopLossConfig.AtrMultiplier ?? 3m;

            if (atr > 0m && gridState.TrailingStopHighWatermark.HasValue)
            {
                var trailingStopPrice = gridState.TrailingStopHighWatermark.Value - (atr * multiplier);

                if (context.CurrentCandle.Close <= trailingStopPrice)
                {
                    var signal = new TradingSignal
                    {
                        SignalType = "TakeProfit",
                        Symbol = context.Symbol,
                        Reason = $"ATR trailing stop triggered (stop: {trailingStopPrice:F2}).",
                        Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["targetPrice"] = context.CurrentCandle.Close,
                            ["size"] = Math.Abs(positionState.Size),
                            ["orderType"] = OrderType.Market.ToString(),
                            ["cancellationReason"] = CancellationReason.TrailingStopTriggered.ToString(),
                            ["gridCycleId"] = "signal"
                        }
                    };

                    gridState.TrailingStopHighWatermark = null;
                    gridState.CandlesSinceEntry = 0;
                    gridState.AtrAtEntry = null;
                    return [signal];
                }
            }
        }

        // Fixed percent stop loss
        if (isFixedStopLoss)
        {
            var stopLossPercent = Math.Abs(stopLossConfig.Value!.Value);
            var stopLossTrigger = positionState.AverageEntryPrice * (1m - (stopLossPercent / 100m));

            if (context.CurrentCandle.Close <= stopLossTrigger)
            {
                gridState.TrailingStopHighWatermark = null;
                gridState.CandlesSinceEntry = 0;
                gridState.AtrAtEntry = null;

                return
                [
                    new TradingSignal
                    {
                        SignalType = "TakeProfit",
                        Symbol = context.Symbol,
                        Reason = "Stop loss triggered.",
                        Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["targetPrice"] = context.CurrentCandle.Close,
                            ["size"] = Math.Abs(positionState.Size),
                            ["orderType"] = OrderType.Market.ToString(),
                            ["cancellationReason"] = CancellationReason.StopLossTriggered.ToString(),
                            ["gridCycleId"] = "signal"
                        }
                    }
                ];
            }
        }

        if (isAtrInitial)
        {
            var multiplier = stopLossConfig.AtrMultiplier ?? 2m;
            var isLong = positionState.Size > 0m;
            var atrAtEntry = gridState.AtrAtEntry.GetValueOrDefault();
            var stopPrice = isLong
                ? positionState.AverageEntryPrice - (atrAtEntry * multiplier)
                : positionState.AverageEntryPrice + (atrAtEntry * multiplier);

            var triggered = isLong
                ? context.CurrentCandle.Close <= stopPrice
                : context.CurrentCandle.Close >= stopPrice;

            if (triggered)
            {
                gridState.TrailingStopHighWatermark = null;
                gridState.CandlesSinceEntry = 0;
                gridState.AtrAtEntry = null;

                return
                [
                    new TradingSignal
                    {
                        SignalType = "TakeProfit",
                        Symbol = context.Symbol,
                        Reason = $"ATR initial stop triggered (stop: {stopPrice:F2}).",
                        Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["targetPrice"] = context.CurrentCandle.Close,
                            ["size"] = Math.Abs(positionState.Size),
                            ["orderType"] = OrderType.Market.ToString(),
                            ["cancellationReason"] = CancellationReason.StopLossTriggered.ToString(),
                            ["gridCycleId"] = "signal"
                        }
                    }
                ];
            }
        }

        // Take profit
        var takeProfitPercent = config.Exit.TakeProfit.Enabled && config.Exit.TakeProfit.Value.HasValue
            ? Math.Abs(config.Exit.TakeProfit.Value.Value)
            : 0m;
        var takeProfitTrigger = positionState.AverageEntryPrice * (1m + (takeProfitPercent / 100m));

        if (takeProfitPercent > 0m && context.CurrentCandle.Close >= takeProfitTrigger)
        {
            gridState.TrailingStopHighWatermark = null;
            gridState.CandlesSinceEntry = 0;
            gridState.AtrAtEntry = null;

            return
            [
                new TradingSignal
                {
                    SignalType = "TakeProfit",
                    Symbol = context.Symbol,
                    Reason = "Take profit triggered.",
                    Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["targetPrice"] = context.CurrentCandle.Close,
                        ["size"] = Math.Abs(positionState.Size),
                        ["orderType"] = OrderType.Market.ToString(),
                        ["cancellationReason"] = CancellationReason.TakeProfitTriggered.ToString(),
                        ["gridCycleId"] = "signal"
                    }
                }
            ];
        }

        return Array.Empty<TradingSignal>();
    }
}