using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Trading.Services;

public sealed class GridController : IGridController
{
    public Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluation evaluation,
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
            var exitSignal = EvaluateExitConditions(context, gridState, positionState, config);
            if (exitSignal is not null)
            {
                return Task.FromResult<IReadOnlyList<TradingSignal>>([exitSignal]);
            }

            if (gridState.Lifecycle == GridLifecycle.Closing)
            {
                return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
            }

            if (gridState.Lifecycle == GridLifecycle.PartiallyFilled)
            {
                var takeProfitPercent = config.Exit.TakeProfit.Enabled && config.Exit.TakeProfit.Value.HasValue
                    ? Math.Abs(config.Exit.TakeProfit.Value.Value)
                    : 0m;
                var takeProfitTrigger = positionState.AverageEntryPrice * (1m + (takeProfitPercent / 100m));

                if (takeProfitPercent > 0m && context.CurrentCandle.Close >= takeProfitTrigger)
                {
                    gridState.Lifecycle = GridLifecycle.Closing;
                    gridState.TrailingStopHighWatermark = null;
                    gridState.CandlesSinceEntry = 0;

                    return Task.FromResult<IReadOnlyList<TradingSignal>>(
                    [
                        new TradingSignal
                        {
                            SignalType = "TakeProfit",
                            Symbol = context.Symbol,
                            Reason = "Take profit triggered (partial fill).",
                            Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["targetPrice"] = context.CurrentCandle.Close,
                                ["size"] = Math.Abs(positionState.Size),
                                ["orderType"] = OrderType.Market.ToString(),
                                ["gridCycleId"] = gridState.GridCycleId ?? "default",
                                ["cancellationReason"] = CancellationReason.TakeProfitTriggered.ToString()
                            }
                        }
                    ]);
                }

                return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
            }

            var tpPercent = config.Exit.TakeProfit.Enabled && config.Exit.TakeProfit.Value.HasValue
                ? Math.Abs(config.Exit.TakeProfit.Value.Value)
                : 0m;
            var tpTrigger = positionState.AverageEntryPrice * (1m + (tpPercent / 100m));

            gridState.Lifecycle = GridLifecycle.Closing;
            gridState.TrailingStopHighWatermark = null;
            gridState.CandlesSinceEntry = 0;

            return Task.FromResult<IReadOnlyList<TradingSignal>>(
            [
                new TradingSignal
                {
                    SignalType = "TakeProfit",
                    Symbol = context.Symbol,
                    Reason = "Take profit active.",
                    Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["targetPrice"] = tpTrigger,
                        ["size"] = Math.Abs(positionState.Size),
                        ["orderType"] = OrderType.Limit.ToString(),
                        ["gridCycleId"] = gridState.GridCycleId ?? "default",
                        ["cancellationReason"] = CancellationReason.TakeProfitTriggered.ToString()
                    }
                }
            ]);
        }

        if (!evaluation.SetupDetected)
        {
            return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
        }

        if (gridState.Lifecycle is not (GridLifecycle.Inactive or GridLifecycle.Closed))
        {
            return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
        }

        var grid = config.Grid ?? throw new InvalidOperationException("Grid configuration is required for grid mode.");
        var gridLevels = Math.Max(1, grid.Levels);
        var entryMode = string.IsNullOrWhiteSpace(grid.EntryMode)
            ? EntryModes.AutoFromSignalCandle
            : grid.EntryMode;
        var anchorPrice = context.CurrentCandle.Close;

        if (string.Equals(entryMode, EntryModes.WaitForLimitPrice, StringComparison.Ordinal))
        {
            if (grid.AnchorPrice is null || context.CurrentCandle.Low > grid.AnchorPrice.Value)
            {
                return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
            }

            anchorPrice = grid.AnchorPrice.Value;
        }

        var gridSpacingPercent = Math.Abs(grid.Spacing);
        var positionSize = PositionSizeResolver.ResolveNotional(config.Risk, context.AccountEquity);

        gridState.GridCycleId = Guid.NewGuid().ToString("N");
        gridState.Lifecycle = GridLifecycle.Deploying;
        gridState.TotalLevels = gridLevels;
        gridState.FilledLevels = 0;

        return Task.FromResult<IReadOnlyList<TradingSignal>>(
        [
            new TradingSignal
            {
                SignalType = "DeployGrid",
                Symbol = context.Symbol,
                Reason = evaluation.Reason,
                Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["anchorPrice"] = anchorPrice,
                    ["gridLevels"] = gridLevels,
                    ["gridSpacingPercent"] = gridSpacingPercent,
                    ["notionalPerLevel"] = positionSize,
                    ["gridCycleId"] = gridState.GridCycleId,
                    ["entryMode"] = entryMode,
                }
            }
        ]);
    }

    private static TradingSignal? EvaluateExitConditions(
        MarketContext context,
        GridState gridState,
        PositionState positionState,
        StrategyConfig config)
    {
        var gridCycleId = gridState.GridCycleId ?? "default";
        var stopLossConfig = config.Exit.StopLoss;
        var isAtrTrailing = stopLossConfig.Enabled && stopLossConfig.Type == ExitRuleType.AtrTrailing;
        var isFixedStopLoss = stopLossConfig.Enabled
            && stopLossConfig.Type != ExitRuleType.AtrTrailing
            && stopLossConfig.Value.HasValue;

        // Update trailing stop high watermark when position is open
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
                return null;
            }

            var atr = context.Indicators?.Atr ?? 0m;
            var multiplier = stopLossConfig.AtrMultiplier ?? 3m;

            if (atr > 0m && gridState.TrailingStopHighWatermark.HasValue)
            {
                var trailingStopPrice = gridState.TrailingStopHighWatermark.Value - (atr * multiplier);

                if (context.CurrentCandle.Close <= trailingStopPrice)
                {
                    gridState.Lifecycle = GridLifecycle.Closing;

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
                            ["gridCycleId"] = gridCycleId,
                            ["cancellationReason"] = CancellationReason.TrailingStopTriggered.ToString()
                        }
                    };

                    gridState.TrailingStopHighWatermark = null;
                    gridState.CandlesSinceEntry = 0;
                    return signal;
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
                gridState.Lifecycle = GridLifecycle.Closing;
                gridState.TrailingStopHighWatermark = null;
                gridState.CandlesSinceEntry = 0;

                return new TradingSignal
                {
                    SignalType = "TakeProfit",
                    Symbol = context.Symbol,
                    Reason = "Stop loss triggered.",
                    Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["targetPrice"] = context.CurrentCandle.Close,
                        ["size"] = Math.Abs(positionState.Size),
                        ["orderType"] = OrderType.Market.ToString(),
                        ["gridCycleId"] = gridCycleId,
                        ["cancellationReason"] = CancellationReason.StopLossTriggered.ToString()
                    }
                };
            }
        }

        return null;
    }
}