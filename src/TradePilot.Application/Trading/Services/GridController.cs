using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Trading.Services;

public sealed class GridController : IGridController
{
    private readonly IExecutionLogger _executionLogger;

    public GridController(IExecutionLogger? executionLogger = null)
    {
        _executionLogger = executionLogger ?? NullExecutionLogger.Instance;
    }

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
            _executionLogger.LogDetail(
                ExecutionLogCategory.ExitCheck,
                $"Position open ({positionState.Size:F4} @ {positionState.AverageEntryPrice:F2}). Checking exit conditions...");

            var exitSignal = EvaluateExitConditions(context, gridState, positionState, config);
            if (exitSignal is not null)
            {
                _executionLogger.LogSummary(
                    ExecutionLogCategory.ExitCheck,
                    $"Exit signal triggered: {exitSignal.Reason}");
                return Task.FromResult<IReadOnlyList<TradingSignal>>([exitSignal]);
            }

            if (gridState.Lifecycle == GridLifecycle.Closing)
            {
                return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
            }

            if (gridState.Lifecycle == GridLifecycle.PartiallyFilled)
            {
                var takeProfitTrigger = ComputeTakeProfitTrigger(
                    positionState.AverageEntryPrice,
                    config.Exit,
                    context.Indicators?.Atr,
                    config.Grid?.BreakdownThreshold);

                if (takeProfitTrigger > 0m && context.CurrentCandle.Close >= takeProfitTrigger)
                {
                    gridState.Lifecycle = GridLifecycle.Closing;
                    gridState.TrailingStopHighWatermark = null;
                    gridState.CandlesSinceEntry = 0;
                    gridState.InitialRDollars = null;
                    gridState.AtrAtEntry = null;

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

            var tpTrigger = ComputeTakeProfitTrigger(
                positionState.AverageEntryPrice,
                config.Exit,
                context.Indicators?.Atr,
                config.Grid?.BreakdownThreshold);

            if (tpTrigger <= 0m)
            {
                return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
            }

            gridState.Lifecycle = GridLifecycle.Closing;
            gridState.TrailingStopHighWatermark = null;
            gridState.CandlesSinceEntry = 0;
            gridState.InitialRDollars = null;
            gridState.AtrAtEntry = null;

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
            _executionLogger.LogDetail(
                ExecutionLogCategory.EntryGate,
                "Grid deployment skipped: no setup detected.");
            return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
        }

        if (gridState.Lifecycle is not (GridLifecycle.Inactive or GridLifecycle.Closed))
        {
            _executionLogger.LogDetail(
                ExecutionLogCategory.GridState,
                $"Grid deployment blocked: lifecycle is {gridState.Lifecycle} (must be Inactive or Closed).");
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
        var stopLossPercent = config.Risk.PositionSizeType == PositionSizeType.RiskBased
            ? StopLossDistanceResolver.Resolve(
                config.Exit.StopLoss,
                context.Indicators?.Atr,
                anchorPrice,
                grid.BreakdownThreshold)
            : null;
        var positionSize = PositionSizeResolver.ResolveNotional(config.Risk, context.AccountEquity, stopLossPercent);
        positionSize *= context.DrawdownScalingFactor;
        var notionalPerLevel = config.Risk.PositionSizeType == PositionSizeType.RiskBased
            ? positionSize / gridLevels
            : positionSize;
        var leverage = config.Risk.AutoLeverage
            && config.Risk.PositionSizeType == PositionSizeType.RiskBased
            && stopLossPercent.HasValue
                ? LeverageCalculator.CalculateLeverage(
                    stopLossPercent.Value,
                    context.MaxLeverage ?? LeverageCalculator.FallbackMaxLeverage)
                : Math.Max(1, (int)Math.Floor(config.Risk.Leverage));
        var isIsolated = config.Risk.PositionSizeType == PositionSizeType.RiskBased;
        var estimatedRiskUsd = EstimateSignalRisk(
            config.Risk,
            notionalPerLevel,
            context.AccountEquity,
            stopLossPercent,
            gridLevels,
            context.DrawdownScalingFactor);

        if (notionalPerLevel <= 0m)
        {
            _executionLogger.LogDetail(
                ExecutionLogCategory.EntryGate,
                "Grid deployment blocked: position sizing returned zero notional.");
            return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
        }

        gridState.GridCycleId = Guid.NewGuid().ToString("N");
        gridState.Lifecycle = GridLifecycle.Deploying;
        gridState.TotalLevels = gridLevels;
        gridState.FilledLevels = 0;
        gridState.InitialRDollars = PositionSizeResolver.ResolveInitialR(config.Risk, context.AccountEquity);
        gridState.AtrAtEntry = config.Exit.StopLoss.Type == ExitRuleType.AtrInitial
            ? context.Indicators?.Atr
            : null;

        _executionLogger.LogSummary(
            ExecutionLogCategory.GridState,
            $"Deploying grid: {gridLevels} levels, ${notionalPerLevel:F2}/level, anchor={anchorPrice:F2}, leverage={leverage}x",
            new Dictionary<string, object>
            {
                ["gridCycleId"] = gridState.GridCycleId,
                ["levels"] = gridLevels,
                ["notionalPerLevel"] = notionalPerLevel,
                ["anchorPrice"] = anchorPrice,
                ["leverage"] = leverage,
                ["estimatedRiskUsd"] = estimatedRiskUsd,
            });

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
                    ["notionalUsd"] = notionalPerLevel,
                    ["gridCycleId"] = gridState.GridCycleId,
                    ["entryMode"] = entryMode,
                    ["leverage"] = leverage,
                    ["isIsolated"] = isIsolated,
                    ["estimatedRiskUsd"] = estimatedRiskUsd,
                }
            }
        ]);
    }

    private static decimal EstimateSignalRisk(
        RiskConfig risk,
        decimal notionalUsd,
        decimal equity,
        decimal? stopLossPercent,
        int gridLevels,
        decimal drawdownScalingFactor)
    {
        if (risk.PositionSizeType == PositionSizeType.RiskBased
            && risk.RiskPerTradePercent.HasValue
            && risk.RiskPerTradePercent.Value > 0m)
        {
            return Math.Max(0m, equity) * (risk.RiskPerTradePercent.Value / 100m) * Math.Max(0m, drawdownScalingFactor);
        }

        var totalNotionalUsd = risk.PositionSizeType == PositionSizeType.RiskBased
            ? notionalUsd * Math.Max(1, gridLevels)
            : notionalUsd * Math.Max(1, gridLevels);

        if (stopLossPercent.HasValue && stopLossPercent.Value > 0m)
        {
            return totalNotionalUsd * (stopLossPercent.Value / 100m);
        }

        var leverage = Math.Max(1m, risk.Leverage);
        return totalNotionalUsd / leverage;
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
        var isAtrInitial = stopLossConfig.Enabled
            && stopLossConfig.Type == ExitRuleType.AtrInitial
            && gridState.AtrAtEntry.HasValue
            && gridState.AtrAtEntry.Value > 0m;
        var isFixedStopLoss = stopLossConfig.Enabled
            && (stopLossConfig.Type != ExitRuleType.AtrTrailing
                && stopLossConfig.Type != ExitRuleType.AtrInitial
                || (stopLossConfig.Type == ExitRuleType.AtrInitial && !isAtrInitial))
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
                    gridState.InitialRDollars = null;
                    gridState.AtrAtEntry = null;

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
                gridState.InitialRDollars = null;
                gridState.AtrAtEntry = null;

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
                gridState.Lifecycle = GridLifecycle.Closing;
                gridState.TrailingStopHighWatermark = null;
                gridState.CandlesSinceEntry = 0;
                gridState.InitialRDollars = null;
                gridState.AtrAtEntry = null;

                return new TradingSignal
                {
                    SignalType = "TakeProfit",
                    Symbol = context.Symbol,
                    Reason = $"ATR initial stop triggered (stop: {stopPrice:F2}).",
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

    // This currently matches the existing long-only grid TP behavior.
    private static decimal ComputeTakeProfitTrigger(
        decimal averageEntryPrice,
        ExitConfig exitConfig,
        decimal? atr,
        decimal? gridBreakdownThreshold)
    {
        var takeProfitConfig = exitConfig.TakeProfit;
        if (!takeProfitConfig.Enabled || !takeProfitConfig.Value.HasValue)
        {
            return 0m;
        }

        if (takeProfitConfig.Type == ExitRuleType.RMultiple)
        {
            var stopLossPercent = StopLossDistanceResolver.Resolve(
                exitConfig.StopLoss,
                atr,
                averageEntryPrice,
                gridBreakdownThreshold);

            if (!stopLossPercent.HasValue || stopLossPercent.Value <= 0m)
            {
                return 0m;
            }

            var rMultiple = Math.Abs(takeProfitConfig.Value.Value);
            return averageEntryPrice * (1m + (stopLossPercent.Value * rMultiple / 100m));
        }

        var percent = Math.Abs(takeProfitConfig.Value.Value);
        return averageEntryPrice * (1m + (percent / 100m));
    }
}