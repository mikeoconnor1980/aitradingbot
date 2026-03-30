using System.Text.Json;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Scheduling;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// Orchestrates a full backtest replay using the shared trading pipeline.
/// </summary>
public sealed class BacktestRunner : IBacktestRunner
{
    private readonly ICandleRepository _candleRepository;
    private readonly IMarketContextBuilder _marketContextBuilder;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IGridController _gridController;
    private readonly IRiskEngine _riskEngine;
    private readonly IPositionManager _positionManager;
    private readonly BacktestExecutionContextAccessor _executionContextAccessor;

    public BacktestRunner(
        ICandleRepository candleRepository,
        IMarketContextBuilder marketContextBuilder,
        IStrategyEngine strategyEngine,
        IGridController gridController,
        IRiskEngine riskEngine,
        IPositionManager positionManager,
        BacktestExecutionContextAccessor executionContextAccessor)
    {
        _candleRepository = candleRepository ?? throw new ArgumentNullException(nameof(candleRepository));
        _marketContextBuilder = marketContextBuilder ?? throw new ArgumentNullException(nameof(marketContextBuilder));
        _strategyEngine = strategyEngine ?? throw new ArgumentNullException(nameof(strategyEngine));
        _gridController = gridController ?? throw new ArgumentNullException(nameof(gridController));
        _riskEngine = riskEngine ?? throw new ArgumentNullException(nameof(riskEngine));
        _positionManager = positionManager ?? throw new ArgumentNullException(nameof(positionManager));
        _executionContextAccessor = executionContextAccessor ?? throw new ArgumentNullException(nameof(executionContextAccessor));
    }

    public Task<BacktestResult> RunAsync(BacktestConfig config, CancellationToken cancellationToken = default)
    {
        return RunAsync(config, onProgress: null, cancellationToken);
    }

    public async Task<BacktestResult> RunAsync(BacktestConfig config, Action<int, int>? onProgress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ValidateConfig(config);

        var auditCollector = config.EnableAuditLog
            ? new BacktestAuditCollector()
            : null;
        IBacktestAuditCollector collector = auditCollector is not null
            ? auditCollector
            : NullBacktestAuditCollector.Instance;
        var executionEngine = new SimulatedExecutionEngine(config.FeeModel);
        var replayEngine = new CandleReplayEngine(_candleRepository);
        var candleClock = new CandleClock();
        var metricsCalculator = new BacktestMetricsCalculator();
        if (_positionManager is BacktestPositionManager btPm)
        {
            btPm.SetAuditCollector(collector);
        }

        var positionManager = _positionManager;
        var scheduler = new StrategyScheduler(
            _marketContextBuilder,
            _strategyEngine,
            _gridController,
            _riskEngine,
            positionManager,
            config.StrategyConfigJson,
            auditCollector: collector);

        _executionContextAccessor.CurrentExecutionEngine = executionEngine;

        try
        {
            var replayData = await replayEngine.LoadAsync(config, cancellationToken);
            var totalCandles = Math.Max(0, replayData.Candles15m.Count - replayData.WarmupEndIndex);
            onProgress?.Invoke(0, totalCandles);
            var tradeLog = new List<BacktestTrade>();
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
                var warmupCandle = replayData.Candles15m[index];
                _executionContextAccessor.CurrentTimestampUtc = warmupCandle.Timestamp;
                _marketContextBuilder.UpdateIndicators(warmupCandle);

                if (auditCollector is not null)
                {
                    var warmupOneHourCandle = CandleReplayEngine.GetLatestClosedCandle(replayData.Candles1h, warmupCandle.Timestamp);
                    var warmupFourHourCandle = CandleReplayEngine.GetLatestClosedCandle(replayData.Candles4h, warmupCandle.Timestamp);
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

            for (var index = replayData.WarmupEndIndex; index < replayData.Candles15m.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var candle = replayData.Candles15m[index];
                _executionContextAccessor.CurrentTimestampUtc = candle.Timestamp;
                var fills = executionEngine.ProcessCandle(candle);

                foreach (var fill in fills)
                {
                    RecordFill(tradeLog, currentGridState, fill);

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

                if (TryCountClosedGridCycle(currentGridState, countedClosedCycles))
                {
                    gridCycles++;

                    if (auditCollector is not null)
                    {
                        LogCompletedGridCycle(auditCollector, trackedCycles, tradeLog, currentGridState, candle.Timestamp);
                    }
                }

                _marketContextBuilder.UpdateIndicators(candle);

                latestOneHourCandle = CandleReplayEngine.GetLatestClosedCandle(replayData.Candles1h, candle.Timestamp);
                latestFourHourCandle = CandleReplayEngine.GetLatestClosedCandle(replayData.Candles4h, candle.Timestamp);

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
                    onProgress?.Invoke(candlesProcessed, totalCandles);
                }
            }

            var metrics = metricsCalculator.Calculate(
                tradeLog,
                equityTimeSeries,
                config.InitialCapital,
                gridCycles,
                Math.Max(0, replayData.Candles15m.Count - replayData.WarmupEndIndex));

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
        ArgumentNullException.ThrowIfNull(config.FeeModel);

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

        // Note: CandleReplayEngine always loads 15m, 1h, 4h regardless of config.Intervals.
        // This validation ensures config explicitly lists the required intervals.
        EnsureRequiredInterval(config.Intervals, "15m");
        EnsureRequiredInterval(config.Intervals, "1h");
        EnsureRequiredInterval(config.Intervals, "4h");

        ArgumentException.ThrowIfNullOrWhiteSpace(config.StrategyConfigJson, nameof(config.StrategyConfigJson));

        try
        {
            using var _ = JsonDocument.Parse(config.StrategyConfigJson);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Strategy config JSON is invalid.", nameof(config.StrategyConfigJson), exception);
        }

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

    private static void RecordFill(List<BacktestTrade> tradeLog, GridState gridState, SimulatedFill fill)
    {
        var gridCycleId = fill.GridCycleId ?? gridState.GridCycleId ?? "default";
        ApplyGridFillState(gridState, fill);

        if (fill.TradeType is TradeType.GridFill or TradeType.HedgeOpen)
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
                TradeType = fill.TradeType
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

        CloseCompatibleTrades(tradeLog, compatibleOpenTrades, fill, gridCycleId);
    }

    private static void AppendOpenTrade(
        List<BacktestTrade> tradeLog,
        SimulatedFill fill,
        string gridCycleId,
        decimal size,
        decimal fee)
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
            TradeType = fill.TradeType
        });
    }

    private static void CloseCompatibleTrades(
        List<BacktestTrade> tradeLog,
        IReadOnlyList<BacktestTrade> compatibleOpenTrades,
        SimulatedFill fill,
        string gridCycleId)
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
                PnL = CalculateTradePnl(openTrade.Side, openTrade.EntryPrice, fill.FillPrice, closedSize),
                Fees = openTrade.Fees + allocatedExitFee,
                TradeType = openTrade.TradeType
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
                    TradeType = openTrade.TradeType
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
            TradeType.TakeProfit => openTrade.TradeType == TradeType.GridFill,
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
                gridState.FilledLevels = 0;
                gridState.Lifecycle = GridLifecycle.Closed;
                break;

            case TradeType.HedgeOpen:
                gridState.Lifecycle = GridLifecycle.Active;
                break;

            case TradeType.HedgeClose:
                gridState.Lifecycle = GridLifecycle.Closed;
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
        if (string.IsNullOrWhiteSpace(fill.GridCycleId) || fill.TradeType != TradeType.TakeProfit)
        {
            return;
        }

        if (!trackedCycles.TryGetValue(fill.GridCycleId, out var trackingState))
        {
            trackingState = new GridCycleTrackingState();
            trackedCycles[fill.GridCycleId] = trackingState;
        }

        if (fill.IsMaker)
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
}