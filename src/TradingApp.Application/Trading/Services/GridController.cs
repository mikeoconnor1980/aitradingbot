using System.Text.Json;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Trading.Services;

public sealed class GridController : IGridController
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluation evaluation,
        MarketContext context,
        GridState gridState,
        PositionState positionState,
        string strategyConfigJson,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(gridState);
        ArgumentNullException.ThrowIfNull(positionState);
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyConfigJson);

        var config = JsonSerializer.Deserialize<GridStrategyConfig>(strategyConfigJson, JsonOptions)
            ?? throw new ArgumentException("Strategy config JSON is invalid.", nameof(strategyConfigJson));

        if (positionState.IsOpen)
        {
            var stopLossPercent = Math.Abs(config.StopLossPercent);
            var takeProfitPercent = Math.Abs(config.TakeProfitPercent);
            var stopLossTrigger = positionState.AverageEntryPrice * (1m - (stopLossPercent / 100m));
            var shouldStopOut = stopLossPercent > 0m && context.CurrentCandle.Close <= stopLossTrigger;
            var orderType = shouldStopOut ? OrderType.Market : OrderType.Limit;
            var targetPrice = shouldStopOut
                ? context.CurrentCandle.Close
                : positionState.AverageEntryPrice * (1m + (takeProfitPercent / 100m));
            var gridCycleId = gridState.GridCycleId ?? "default";

            gridState.Lifecycle = GridLifecycle.Closing;

            return Task.FromResult<IReadOnlyList<TradingSignal>>(
            [
                new TradingSignal
                {
                    SignalType = "TakeProfit",
                    Symbol = context.Symbol,
                    Reason = shouldStopOut ? "Stop loss triggered." : "Take profit active.",
                    Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["targetPrice"] = targetPrice,
                        ["size"] = Math.Abs(positionState.Size),
                        ["orderType"] = orderType.ToString(),
                        ["gridCycleId"] = gridCycleId
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

        var gridLevels = Math.Max(1, config.GridLevels);
        var entryMode = string.IsNullOrWhiteSpace(config.EntryMode)
            ? BacktestEntryModes.AutoFromSignalCandle
            : config.EntryMode;
        var anchorPrice = context.CurrentCandle.Close;

        if (string.Equals(entryMode, BacktestEntryModes.WaitForLimitPrice, StringComparison.Ordinal))
        {
            if (config.ManualAnchorPrice is null || context.CurrentCandle.Low > config.ManualAnchorPrice.Value)
            {
                return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
            }

            anchorPrice = config.ManualAnchorPrice.Value;
        }

        var gridSpacingPercent = Math.Abs(config.GridSpacing);
        var positionSize = Math.Abs(config.PositionSize);

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
                }
            }
        ]);
    }
}