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
            var stopLossPercent = config.Exit.StopLoss.Enabled && config.Exit.StopLoss.Value.HasValue
                ? Math.Abs(config.Exit.StopLoss.Value.Value)
                : 0m;
            var takeProfitPercent = config.Exit.TakeProfit.Enabled && config.Exit.TakeProfit.Value.HasValue
                ? Math.Abs(config.Exit.TakeProfit.Value.Value)
                : 0m;
            var stopLossTrigger = positionState.AverageEntryPrice * (1m - (stopLossPercent / 100m));
            var takeProfitTrigger = positionState.AverageEntryPrice * (1m + (takeProfitPercent / 100m));
            var shouldStopOut = stopLossPercent > 0m && context.CurrentCandle.Close <= stopLossTrigger;
            var gridCycleId = gridState.GridCycleId ?? "default";

            if (shouldStopOut)
            {
                gridState.Lifecycle = GridLifecycle.Closing;

                return Task.FromResult<IReadOnlyList<TradingSignal>>(
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
                            ["gridCycleId"] = gridCycleId,
                            ["cancellationReason"] = CancellationReason.StopLossTriggered.ToString()
                        }
                    }
                ]);
            }

            if (gridState.Lifecycle == GridLifecycle.Closing)
            {
                return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
            }

            if (gridState.Lifecycle == GridLifecycle.PartiallyFilled)
            {
                if (takeProfitPercent > 0m && context.CurrentCandle.Close >= takeProfitTrigger)
                {
                    gridState.Lifecycle = GridLifecycle.Closing;

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
                                ["gridCycleId"] = gridCycleId,
                                ["cancellationReason"] = CancellationReason.TakeProfitTriggered.ToString()
                            }
                        }
                    ]);
                }

                return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
            }

            gridState.Lifecycle = GridLifecycle.Closing;

            return Task.FromResult<IReadOnlyList<TradingSignal>>(
            [
                new TradingSignal
                {
                    SignalType = "TakeProfit",
                    Symbol = context.Symbol,
                    Reason = "Take profit active.",
                    Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["targetPrice"] = takeProfitTrigger,
                        ["size"] = Math.Abs(positionState.Size),
                        ["orderType"] = OrderType.Limit.ToString(),
                        ["gridCycleId"] = gridCycleId,
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
}