using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Scheduling;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;
using TradingApp.Domain.Trading;
using Microsoft.Extensions.Options;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// Orchestrates a full backtest replay using the shared trading pipeline.
/// </summary>
public sealed class BacktestRunner : IBacktestRunner
{
    private sealed class TradeExcursionTracker
    {
        public decimal BestPnL { get; set; }

        public decimal WorstPnL { get; set; }
    }

    private readonly ICandleRepository _candleRepository;
    private readonly IMarketContextBuilder _marketContextBuilder;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IGridController _gridController;
    private readonly IRiskEngine _riskEngine;
    private readonly IPositionManager _positionManager;
    private readonly BacktestExecutionContextAccessor _executionContextAccessor;
    private readonly ISignalController _signalController;
    private readonly RiskLimitsConfig _riskLimits;

    public BacktestRunner(
        ICandleRepository candleRepository,
        IMarketContextBuilder marketContextBuilder,
        IStrategyEngine strategyEngine,
        IGridController gridController,
        IRiskEngine riskEngine,
        IPositionManager positionManager,
        BacktestExecutionContextAccessor executionContextAccessor,
        ISignalController signalController,
        IOptions<RiskLimitsConfig>? riskLimits = null)
    {
        _candleRepository = candleRepository ?? throw new ArgumentNullException(nameof(candleRepository));
        _marketContextBuilder = marketContextBuilder ?? throw new ArgumentNullException(nameof(marketContextBuilder));
        _strategyEngine = strategyEngine ?? throw new ArgumentNullException(nameof(strategyEngine));
        _gridController = gridController ?? throw new ArgumentNullException(nameof(gridController));
        _riskEngine = riskEngine ?? throw new ArgumentNullException(nameof(riskEngine));
        _positionManager = positionManager ?? throw new ArgumentNullException(nameof(positionManager));
        _executionContextAccessor = executionContextAccessor ?? throw new ArgumentNullException(nameof(executionContextAccessor));
        _signalController = signalController ?? throw new ArgumentNullException(nameof(signalController));
        _riskLimits = riskLimits?.Value ?? new RiskLimitsConfig { DrawdownTiers = RiskLimitsConfig.DefaultDrawdownTiers.ToArray() };
    }

    public Task<BacktestResult> RunAsync(BacktestConfig config, CancellationToken cancellationToken = default)
    {
        return RunCoreAsync(config, preloadedData: null, onProgress: null, cancellationToken);
    }

    public Task<BacktestResult> RunAsync(BacktestConfig config, Action<int, int, long>? onProgress, CancellationToken cancellationToken = default)
    {
        return RunCoreAsync(config, preloadedData: null, onProgress, cancellationToken);
    }

    public Task<BacktestResult> RunAsync(BacktestConfig config, ReplayData preloadedData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preloadedData);
        return RunCoreAsync(config, preloadedData, onProgress: null, cancellationToken);
    }

    private async Task<BacktestResult> RunCoreAsync(BacktestConfig config, ReplayData? preloadedData, Action<int, int, long>? onProgress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ValidateConfig(config);

        var auditCollector = config.EnableAuditLog
            ? new BacktestAuditCollector()
            : null;
        IBacktestAuditCollector collector = auditCollector is not null
            ? auditCollector
            : NullBacktestAuditCollector.Instance;
        var executionEngine = new SimulatedExecutionEngine(config.Execution.FeeModel);
        executionEngine.SetMaxLeverage(config.Symbol, LeverageCalculator.FallbackMaxLeverage);
        var replayEngine = new CandleReplayEngine(_candleRepository);
        var candleClock = new CandleClock();
        var metricsCalculator = new BacktestMetricsCalculator();
        if (_positionManager is BacktestPositionManager btPm)
        {
            btPm.SetAuditCollector(collector);
        }

        var positionManager = _positionManager;
        var triggerTimeframe = config.TriggerTimeframe;
        var scheduler = new StrategyScheduler(
            _marketContextBuilder,
            _strategyEngine,
            _gridController,
            _riskEngine,
            positionManager,
            config.Strategy,
            triggerTimeframe: triggerTimeframe,
            auditCollector: collector,
            signalController: _signalController,
            initialCapital: config.InitialCapital,
            executionContextAccessor: _executionContextAccessor,
            drawdownTiers: _riskLimits.DrawdownTiers);

        _executionContextAccessor.CurrentExecutionEngine = executionEngine;

        try
        {
            var replayData = preloadedData ?? await replayEngine.LoadAsync(config, cancellationToken);
            var triggerCandles = replayData.TriggerCandles;
            var totalCandles = Math.Max(0, triggerCandles.Count - replayData.WarmupEndIndex);
            onProgress?.Invoke(0, totalCandles, config.StartDateUtc);
            var tradeLog = new List<BacktestTrade>();
            var excursionTrackers = new Dictionary<string, TradeExcursionTracker>(StringComparer.Ordinal);
            var equityTimeSeries = new List<EquitySnapshot>();
            var currentGridState = scheduler.GetGridState();
            var countedClosedCycles = new HashSet<string>(StringComparer.Ordinal);
            var trackedCycles = new Dictionary<string, GridCycleTrackingState>(StringComparer.Ordinal);
            var gridCycles = 0;
            Candle? latestOneHourCandle = null;
            Candle? latestFourHourCandle = null;

            candleClock.CandleClosed += evt => scheduler.HandleCandleClosedAsync(
                evt,
                latestOneHourCandle,
                latestFourHourCandle,
                cancellationToken);

            for (var index = 0; index < replayData.WarmupEndIndex; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var warmupCandle = triggerCandles[index];
                _executionContextAccessor.CurrentTimestampUtc = warmupCandle.Timestamp;
                _marketContextBuilder.UpdateIndicators(warmupCandle);

                if (auditCollector is not null)
                {
                    var warmupOneHourCandle = ResolveOneHourCandle(replayData, warmupCandle, triggerTimeframe);
                    var warmupFourHourCandle = ResolveFourHourCandle(replayData, warmupCandle, triggerTimeframe);
                    var warmupContext = _marketContextBuilder.Build(
                        warmupCandle,
                        warmupOneHourCandle,
                        warmupFourHourCandle);

                    auditCollector.LogCandleEvaluation(new CandleEvaluationEntry
                    {
                        TimestampUtc = warmupCandle.Timestamp,
                        Open = warmupCandle.Open,
                        High = warmupCandle.High,
                        Low = warmupCandle.Low,
                        Close = warmupCandle.Close,
                        Volume = warmupCandle.Volume,
                        IsWarmup = true,
                        EmaFast = warmupContext.Indicators?.EmaFast ?? 0m,
                        EmaSlow = warmupContext.Indicators?.EmaSlow ?? 0m,
                        EmaTrend = warmupContext.Indicators?.EmaTrend ?? 0m,
                        Rsi = warmupContext.Indicators?.Rsi ?? 0m,
                        Atr = warmupContext.Indicators?.Atr ?? 0m,
                        SetupDetected = false,
                        GridLifecycleState = GridLifecycle.Inactive.ToString(),
                        PositionSize = 0m,
                        PositionAvgEntry = 0m,
                        SignalsEmitted = [],
                        GridCycleId = null
                    });
                }
            }

            for (var index = replayData.WarmupEndIndex; index < triggerCandles.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var candle = triggerCandles[index];
                _executionContextAccessor.CurrentTimestampUtc = candle.Timestamp;
                var fills = executionEngine.ProcessCandle(candle);

                foreach (var fill in fills)
                {
                    RecordFill(tradeLog, currentGridState, fill, excursionTrackers);

                    collector.LogOrderEvent(new OrderEventEntry
                    {
                        TimestampUtc = fill.FillTimeUtc,
                        EventType = OrderEventType.Filled,
                        OrderId = fill.OrderId,
                        Side = fill.Side.ToString(),
                        OrderType = fill.IsMaker ? OrderType.Limit.ToString() : OrderType.Market.ToString(),
                        Price = fill.FillPrice,
                        Size = fill.Size,
                        FillPrice = fill.FillPrice,
                        Fee = fill.Fee,
                        IsMaker = fill.IsMaker,
                        GridCycleId = fill.GridCycleId ?? currentGridState.GridCycleId ?? "default"
                    });

                    TrackCycleExit(trackedCycles, fill);
                }

                UpdateTradeExcursions(tradeLog, excursionTrackers, candle);

                if (TryCountClosedGridCycle(currentGridState, countedClosedCycles))
                {
                    gridCycles++;

                    if (auditCollector is not null)
                    {
                        LogCompletedGridCycle(auditCollector, trackedCycles, tradeLog, currentGridState, candle.Timestamp);
                    }
                }

                _marketContextBuilder.UpdateIndicators(candle);

                latestOneHourCandle = ResolveOneHourCandle(replayData, candle, triggerTimeframe);
                latestFourHourCandle = ResolveFourHourCandle(replayData, candle, triggerTimeframe);

                var position = executionEngine.GetPosition();
                scheduler.UpdateState(
                    currentGridState,
                    new PositionState
                    {
                        Symbol = config.Symbol,
                        Size = position.Size,
                        AverageEntryPrice = position.AverageEntryPrice,
                        UnrealisedPnL = position.UnrealisedPnL
                    });

                await candleClock.ProcessCandleAsync(candle);

                currentGridState = scheduler.GetGridState();
                TrackGridCycle(trackedCycles, currentGridState, executionEngine.GetOpenOrders(), candle);

                if (TryCountClosedGridCycle(currentGridState, countedClosedCycles))
                {
                    gridCycles++;

                    if (auditCollector is not null)
                    {
                        LogCompletedGridCycle(auditCollector, trackedCycles, tradeLog, currentGridState, candle.Timestamp);
                    }
                }

                var simulatedPosition = executionEngine.GetPosition();
                var currentEquity = config.InitialCapital + simulatedPosition.RealisedPnL + simulatedPosition.UnrealisedPnL;
                equityTimeSeries.Add(new EquitySnapshot(candle.Timestamp, currentEquity));

                var candlesProcessed = index - replayData.WarmupEndIndex + 1;
                if (candlesProcessed % 100 == 0 || candlesProcessed == totalCandles)
                {
                    onProgress?.Invoke(candlesProcessed, totalCandles, candle.Timestamp);
                }
            }

            var metrics = metricsCalculator.Calculate(
                tradeLog,
                equityTimeSeries,
                config.InitialCapital,
                gridCycles,
                Math.Max(0, triggerCandles.Count - replayData.WarmupEndIndex));

            return new BacktestResult
            {
                TotalTrades = metrics.TotalTrades,
                WinningTrades = metrics.WinningTrades,
                LosingTrades = metrics.LosingTrades,
                WinRate = metrics.WinRate,
                TotalPnL = metrics.TotalPnL,
                MaxDrawdownAbsolute = metrics.MaxDrawdownAbsolute,
                MaxDrawdownPercent = metrics.MaxDrawdownPercent,
                AverageTradePnL = metrics.AverageTradePnL,
                AverageHoldTime = metrics.AverageHoldTime,
                HedgesOpened = metrics.HedgesOpened,
                TotalFeesPaid = metrics.TotalFeesPaid,
                GridCycles = metrics.GridCycles,
                CandlesReplayed = metrics.CandlesReplayed,
                FinalEquity = metrics.FinalEquity,
                HeatBlockedSignalCount = _riskEngine is BacktestRiskEngine backtestRiskEngine
                    ? backtestRiskEngine.HeatBlockedSignalCount
                    : 0,
                DrawdownBlockedSignalCount = _riskEngine is BacktestRiskEngine drawdownRiskEngine
                    ? drawdownRiskEngine.DrawdownBlockedSignalCount
                    : 0,
                EquityTimeSeries = metrics.EquityTimeSeries,
                TradeLog = metrics.TradeLog,
                CandleEvaluationLog = auditCollector?.CandleEvaluations,
                OrderEventLog = auditCollector?.OrderEvents,
                GridCycleLog = auditCollector?.GridCycles
            };
        }
        finally
        {
            _executionContextAccessor.CurrentExecutionEngine = null;
            _executionContextAccessor.CurrentTimestampUtc = 0;
        }
    }

    private static void ValidateConfig(BacktestConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Symbol, nameof(config.Symbol));
        ArgumentNullException.ThrowIfNull(config.Strategy);
        ArgumentNullException.ThrowIfNull(config.Execution);
        ArgumentNullException.ThrowIfNull(config.Execution.FeeModel);

        if (config.StartDateUtc >= config.EndDateUtc)
        {
            throw new ArgumentException("Start date must be before end date.");
        }

        if (config.InitialCapital <= 0)
        {
            throw new ArgumentException("Initial capital must be greater than zero.");
        }

        if (config.Intervals is null || config.Intervals.Count == 0)
        {
            throw new ArgumentException("At least one interval must be specified.");
        }

        // Note: CandleReplayEngine loads 15m, 1h, 4h for context regardless of trigger timeframe.
        // Ensure the config lists required context intervals.
        EnsureRequiredInterval(config.Intervals, "1h");
        EnsureRequiredInterval(config.Intervals, "4h");

        if (config.WarmupPeriod < 0)
        {
            throw new ArgumentException("Warmup period cannot be negative.");
        }
    }

    private static void EnsureRequiredInterval(IReadOnlyList<string> intervals, string requiredInterval)
    {
        if (!intervals.Contains(requiredInterval, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"{requiredInterval} interval is required for strategy evaluation.");
        }
    }

    private static void RecordFill(
        List<BacktestTrade> tradeLog,
        GridState gridState,
        SimulatedFill fill,
        Dictionary<string, TradeExcursionTracker> excursionTrackers)
    {
        var gridCycleId = fill.GridCycleId ?? gridState.GridCycleId ?? "default";
        ApplyGridFillState(gridState, fill);

        if (fill.TradeType is TradeType.GridFill or TradeType.HedgeOpen or TradeType.SignalEntry)
        {
            tradeLog.Add(new BacktestTrade
            {
                TradeId = fill.OrderId,
                GridCycleId = gridCycleId,
                EntryTimeUtc = fill.FillTimeUtc,
                EntryPrice = fill.FillPrice,
                ExitTimeUtc = null,
                ExitPrice = null,
                Side = fill.Side,
                Size = fill.Size,
                PnL = null,
                Fees = fill.Fee,
                TradeType = fill.TradeType,
                InitialRDollars = gridState.InitialRDollars
            });

            return;
        }

        var compatibleOpenTrades = tradeLog
            .Where(trade =>
                trade.ExitTimeUtc is null &&
                string.Equals(trade.GridCycleId, gridCycleId, StringComparison.Ordinal) &&
                IsCompatibleExit(trade, fill.TradeType))
            .OrderBy(trade => trade.EntryTimeUtc)
            .ToList();

        if (compatibleOpenTrades.Count == 0)
        {
            AppendOpenTrade(tradeLog, fill, gridCycleId, fill.Size, fill.Fee);

            return;
        }

        CloseCompatibleTrades(tradeLog, compatibleOpenTrades, fill, gridCycleId, excursionTrackers);
    }

    private static void AppendOpenTrade(
        List<BacktestTrade> tradeLog,
        SimulatedFill fill,
        string gridCycleId,
        decimal size,
        decimal fee,
        decimal? initialRDollars = null)
    {
        tradeLog.Add(new BacktestTrade
        {
            TradeId = fill.OrderId,
            GridCycleId = gridCycleId,
            EntryTimeUtc = fill.FillTimeUtc,
            EntryPrice = fill.FillPrice,
            ExitTimeUtc = null,
            ExitPrice = null,
            Side = fill.Side,
            Size = size,
            PnL = null,
            Fees = fee,
            TradeType = fill.TradeType,
            InitialRDollars = initialRDollars
        });
    }

    private static void CloseCompatibleTrades(
        List<BacktestTrade> tradeLog,
        IReadOnlyList<BacktestTrade> compatibleOpenTrades,
        SimulatedFill fill,
        string gridCycleId,
        Dictionary<string, TradeExcursionTracker> excursionTrackers)
    {
        var remainingSize = fill.Size;
        var remainingFee = fill.Fee;

        foreach (var openTrade in compatibleOpenTrades)
        {
            if (remainingSize <= 0m)
            {
                break;
            }

            var closedSize = Math.Min(openTrade.Size, remainingSize);
            var allocatedExitFee = fill.Size > 0m
                ? decimal.Round(fill.Fee * (closedSize / fill.Size), 12, MidpointRounding.AwayFromZero)
                : 0m;
            var pnl = CalculateTradePnl(openTrade.Side, openTrade.EntryPrice, fill.FillPrice, closedSize);
            var (rMultipleResult, mfe, mae) = ResolveTradeRMetrics(
                openTrade,
                pnl,
                excursionTrackers,
                removeTracker: closedSize == openTrade.Size);

            var pairedTrade = new BacktestTrade
            {
                TradeId = openTrade.TradeId,
                GridCycleId = openTrade.GridCycleId,
                EntryTimeUtc = openTrade.EntryTimeUtc,
                EntryPrice = openTrade.EntryPrice,
                ExitTimeUtc = fill.FillTimeUtc,
                ExitPrice = fill.FillPrice,
                Side = openTrade.Side,
                Size = closedSize,
                PnL = pnl,
                Fees = openTrade.Fees + allocatedExitFee,
                TradeType = openTrade.TradeType,
                ExitReason = fill.CloseReason?.ToString(),
                InitialRDollars = openTrade.InitialRDollars,
                RMultipleResult = rMultipleResult,
                MFE = mfe,
                MAE = mae
            };

            var openTradeIndex = tradeLog.IndexOf(openTrade);
            if (closedSize == openTrade.Size)
            {
                tradeLog[openTradeIndex] = pairedTrade;
            }
            else
            {
                var remainingOpenTrade = new BacktestTrade
                {
                    TradeId = openTrade.TradeId,
                    GridCycleId = openTrade.GridCycleId,
                    EntryTimeUtc = openTrade.EntryTimeUtc,
                    EntryPrice = openTrade.EntryPrice,
                    ExitTimeUtc = null,
                    ExitPrice = null,
                    Side = openTrade.Side,
                    Size = openTrade.Size - closedSize,
                    PnL = null,
                    Fees = openTrade.Fees,
                    TradeType = openTrade.TradeType,
                    InitialRDollars = openTrade.InitialRDollars
                };

                tradeLog[openTradeIndex] = remainingOpenTrade;
                tradeLog.Insert(openTradeIndex + 1, pairedTrade);
            }

            remainingSize -= closedSize;
            remainingFee -= allocatedExitFee;
        }

        if (remainingSize > 0m)
        {
            AppendOpenTrade(tradeLog, fill, gridCycleId, remainingSize, Math.Max(remainingFee, 0m));
        }
    }

    private static bool IsCompatibleExit(BacktestTrade openTrade, TradeType exitTradeType)
    {
        return exitTradeType switch
        {
            TradeType.TakeProfit => openTrade.TradeType is TradeType.GridFill or TradeType.SignalEntry,
            TradeType.HedgeClose => openTrade.TradeType == TradeType.HedgeOpen,
            _ => false
        };
    }

    private static decimal CalculateTradePnl(OrderSide side, decimal entryPrice, decimal exitPrice, decimal size)
    {
        return side == OrderSide.Buy
            ? (exitPrice - entryPrice) * size
            : (entryPrice - exitPrice) * size;
    }

    private static void UpdateTradeExcursions(
        List<BacktestTrade> tradeLog,
        Dictionary<string, TradeExcursionTracker> trackers,
        Candle candle)
    {
        foreach (var trade in tradeLog)
        {
            if (trade.ExitTimeUtc is not null || !trade.InitialRDollars.HasValue || trade.InitialRDollars.Value <= 0m)
            {
                continue;
            }

            if (!trackers.TryGetValue(trade.TradeId, out var tracker))
            {
                tracker = new TradeExcursionTracker();
                trackers[trade.TradeId] = tracker;
            }

            decimal bestPnl;
            decimal worstPnl;

            if (trade.Side == OrderSide.Buy)
            {
                bestPnl = (candle.High - trade.EntryPrice) * trade.Size;
                worstPnl = (candle.Low - trade.EntryPrice) * trade.Size;
            }
            else
            {
                bestPnl = (trade.EntryPrice - candle.Low) * trade.Size;
                worstPnl = (trade.EntryPrice - candle.High) * trade.Size;
            }

            tracker.BestPnL = Math.Max(tracker.BestPnL, bestPnl);
            tracker.WorstPnL = Math.Min(tracker.WorstPnL, worstPnl);
        }
    }

    private static (decimal? RMultipleResult, decimal? MFE, decimal? MAE) ResolveTradeRMetrics(
        BacktestTrade openTrade,
        decimal pnl,
        Dictionary<string, TradeExcursionTracker> excursionTrackers,
        bool removeTracker)
    {
        var initialR = openTrade.InitialRDollars;
        if (!initialR.HasValue || initialR.Value <= 0m)
        {
            return (null, null, null);
        }

        var rMultipleResult = decimal.Round(pnl / initialR.Value, 4);
        decimal? mfe = null;
        decimal? mae = null;

        if (excursionTrackers.TryGetValue(openTrade.TradeId, out var tracker))
        {
            mfe = decimal.Round(tracker.BestPnL / initialR.Value, 4);
            mae = decimal.Round(tracker.WorstPnL / initialR.Value, 4);

            if (removeTracker)
            {
                excursionTrackers.Remove(openTrade.TradeId);
            }
        }

        return (rMultipleResult, mfe, mae);
    }

    private static void ApplyGridFillState(GridState gridState, SimulatedFill fill)
    {
        switch (fill.TradeType)
        {
            case TradeType.GridFill:
                gridState.FilledLevels = Math.Min(gridState.TotalLevels, gridState.FilledLevels + 1);
                gridState.Lifecycle = gridState.FilledLevels >= gridState.TotalLevels
                    ? GridLifecycle.FullyFilled
                    : GridLifecycle.PartiallyFilled;
                break;

            case TradeType.TakeProfit:
                if (string.Equals(fill.GridCycleId, "signal", StringComparison.Ordinal))
                {
                    break;
                }

                gridState.FilledLevels = 0;
                gridState.Lifecycle = GridLifecycle.Closed;
                gridState.InitialRDollars = null;
                break;

            case TradeType.HedgeOpen:
                gridState.Lifecycle = GridLifecycle.Active;
                break;

            case TradeType.HedgeClose:
                gridState.Lifecycle = GridLifecycle.Closed;
                gridState.InitialRDollars = null;
                break;

            case TradeType.SignalEntry:
                break;
        }
    }

    private static bool TryCountClosedGridCycle(GridState gridState, ISet<string> countedClosedCycles)
    {
        if (gridState.Lifecycle != GridLifecycle.Closed)
        {
            return false;
        }

        var cycleKey = gridState.GridCycleId ?? "closed-without-cycle-id";
        return countedClosedCycles.Add(cycleKey);
    }

    private static void TrackGridCycle(
        IDictionary<string, GridCycleTrackingState> trackedCycles,
        GridState gridState,
        IReadOnlyList<SimulatedOrder> openOrders,
        Candle candle)
    {
        if (string.IsNullOrWhiteSpace(gridState.GridCycleId))
        {
            return;
        }

        var cycleId = gridState.GridCycleId;
        var cycleOrders = openOrders
            .Where(order => string.Equals(order.GridCycleId, cycleId, StringComparison.Ordinal))
            .ToList();

        if (!trackedCycles.TryGetValue(cycleId, out var trackingState))
        {
            if (cycleOrders.Count == 0)
            {
                return;
            }

            var levelOrders = cycleOrders
                .Where(order => order.TradeType == TradeType.GridFill)
                .OrderBy(order => order.Price)
                .ToList();

            trackingState = new GridCycleTrackingState
            {
                DeployTimestampUtc = candle.Timestamp,
                AnchorPrice = levelOrders.MaxBy(order => order.Price)?.AnchorPrice ?? candle.Close,
                LevelsPlaced = levelOrders.Count,
                LevelPrices = levelOrders.Select(order => order.Price).ToList(),
            };
            trackedCycles[cycleId] = trackingState;
        }

        var takeProfitOrder = cycleOrders.FirstOrDefault(order => order.TradeType == TradeType.TakeProfit);
        if (takeProfitOrder is not null && takeProfitOrder.OrderType == OrderType.Limit)
        {
            trackingState.TakeProfitPrice = takeProfitOrder.Price;
        }
    }

    private static void TrackCycleExit(IDictionary<string, GridCycleTrackingState> trackedCycles, SimulatedFill fill)
    {
        if (string.IsNullOrWhiteSpace(fill.GridCycleId)
            || fill.TradeType != TradeType.TakeProfit
            || string.Equals(fill.GridCycleId, "signal", StringComparison.Ordinal))
        {
            return;
        }

        if (!trackedCycles.TryGetValue(fill.GridCycleId, out var trackingState))
        {
            trackingState = new GridCycleTrackingState();
            trackedCycles[fill.GridCycleId] = trackingState;
        }

        if (fill.CloseReason == CancellationReason.TakeProfitTriggered)
        {
            trackingState.ExitReason = "TakeProfit";
            trackingState.TakeProfitPrice = fill.FillPrice;
        }
        else if (fill.CloseReason == CancellationReason.LiquidationTriggered)
        {
            trackingState.ExitReason = "Liquidation";
            trackingState.StopLossPrice = fill.FillPrice;
        }
        else if (fill.CloseReason == CancellationReason.StopLossTriggered)
        {
            trackingState.ExitReason = "StopLoss";
            trackingState.StopLossPrice = fill.FillPrice;
        }
        else if (fill.IsMaker)
        {
            trackingState.ExitReason = "TakeProfit";
            trackingState.TakeProfitPrice = fill.FillPrice;
        }
        else
        {
            trackingState.ExitReason = "StopLoss";
            trackingState.StopLossPrice = fill.FillPrice;
        }
    }

    private static void LogCompletedGridCycle(
        BacktestAuditCollector auditCollector,
        IReadOnlyDictionary<string, GridCycleTrackingState> trackedCycles,
        IReadOnlyList<BacktestTrade> tradeLog,
        GridState gridState,
        long closeTimestampUtc)
    {
        var cycleId = gridState.GridCycleId;
        if (string.IsNullOrWhiteSpace(cycleId))
        {
            return;
        }

        trackedCycles.TryGetValue(cycleId, out var trackingState);
        var cycleTrades = tradeLog
            .Where(trade => string.Equals(trade.GridCycleId, cycleId, StringComparison.Ordinal))
            .ToList();
        var filledLevels = cycleTrades.Count(trade => trade.TradeType == TradeType.GridFill);

        auditCollector.LogGridCycleCompleted(new GridCycleEntry
        {
            GridCycleId = cycleId,
            DeployTimestampUtc = trackingState?.DeployTimestampUtc ?? closeTimestampUtc,
            AnchorPrice = trackingState?.AnchorPrice ?? 0m,
            LevelsPlaced = trackingState?.LevelsPlaced ?? 0,
            LevelPrices = trackingState?.LevelPrices ?? [],
            LevelsFilled = filledLevels,
            TakeProfitPrice = trackingState?.TakeProfitPrice ?? 0m,
            StopLossPrice = trackingState?.StopLossPrice,
            ExitReason = trackingState?.ExitReason ?? "Unknown",
            CyclePnl = cycleTrades.Sum(trade => trade.PnL ?? 0m),
            CycleDurationMs = Math.Max(0, closeTimestampUtc - (trackingState?.DeployTimestampUtc ?? closeTimestampUtc)),
            CloseTimestampUtc = closeTimestampUtc
        });
    }

    private sealed class GridCycleTrackingState
    {
        public long DeployTimestampUtc { get; init; }

        public decimal AnchorPrice { get; init; }

        public int LevelsPlaced { get; init; }

        public List<decimal> LevelPrices { get; init; } = [];

        public decimal TakeProfitPrice { get; set; }

        public decimal? StopLossPrice { get; set; }

        public string ExitReason { get; set; } = "Unknown";
    }

    /// <summary>
    /// Resolves the latest closed 1h candle for context, taking into account
    /// the trigger timeframe. If trigger IS 1h, the current candle is the 1h candle.
    /// </summary>
    private static Candle? ResolveOneHourCandle(ReplayData replayData, Candle currentCandle, string triggerTimeframe)
    {
        return string.Equals(triggerTimeframe, "1h", StringComparison.OrdinalIgnoreCase)
            ? currentCandle
            : CandleReplayEngine.GetLatestClosedCandle(replayData.Candles1h, currentCandle.Timestamp);
    }

    /// <summary>
    /// Resolves the latest closed 4h candle for context, taking into account
    /// the trigger timeframe. If trigger IS 4h, the current candle is the 4h candle.
    /// </summary>
    private static Candle? ResolveFourHourCandle(ReplayData replayData, Candle currentCandle, string triggerTimeframe)
    {
        return string.Equals(triggerTimeframe, "4h", StringComparison.OrdinalIgnoreCase)
            ? currentCandle
            : CandleReplayEngine.GetLatestClosedCandle(replayData.Candles4h, currentCandle.Timestamp);
    }
}