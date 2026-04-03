using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Trading.Services;

/// <summary>
/// Processes signal-mode strategy evaluation and emits position entry and exit signals.
/// </summary>
public sealed class SignalController : ISignalController
{
    public Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluation evaluation,
        MarketContext context,
        PositionState positionState,
        IStrategyConfig strategyConfig,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(context);
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
            return Task.FromResult(EvaluateExitConditions(context, positionState, config));
        }

        if (!evaluation.SetupDetected)
        {
            return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
        }

        return Task.FromResult(EmitOpenPosition(context, config, evaluation.Reason));
    }

    private static IReadOnlyList<TradingSignal> EmitOpenPosition(
        MarketContext context,
        StrategyConfig config,
        string? reason)
    {
        var notional = PositionSizeResolver.ResolveNotional(config.Risk, context.AccountEquity);
        var entryPrice = context.CurrentCandle.Close;
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
                    ["notional"] = notional,
                    ["orderType"] = OrderType.Market.ToString(),
                    ["gridCycleId"] = "signal"
                }
            }
        ];
    }

    private static IReadOnlyList<TradingSignal> EvaluateExitConditions(
        MarketContext context,
        PositionState positionState,
        StrategyConfig config)
    {
        var stopLossPercent = config.Exit.StopLoss.Enabled && config.Exit.StopLoss.Value.HasValue
            ? Math.Abs(config.Exit.StopLoss.Value.Value)
            : 0m;
        var takeProfitPercent = config.Exit.TakeProfit.Enabled && config.Exit.TakeProfit.Value.HasValue
            ? Math.Abs(config.Exit.TakeProfit.Value.Value)
            : 0m;

        var stopLossTrigger = positionState.AverageEntryPrice * (1m - (stopLossPercent / 100m));
        var takeProfitTrigger = positionState.AverageEntryPrice * (1m + (takeProfitPercent / 100m));

        if (stopLossPercent > 0m && context.CurrentCandle.Close <= stopLossTrigger)
        {
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

        if (takeProfitPercent > 0m && context.CurrentCandle.Close >= takeProfitTrigger)
        {
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