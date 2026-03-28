using System.Text.Json;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Scheduling;
using TradingApp.Application.Trading.Models;
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

        var executionEngine = new SimulatedExecutionEngine(config.FeeModel);
        var replayEngine = new CandleReplayEngine(_candleRepository);
        var candleClock = new CandleClock();
        var metricsCalculator = new BacktestMetricsCalculator();
        var scheduler = new StrategyScheduler(
            _marketContextBuilder,
            _strategyEngine,
            _gridController,
            _riskEngine,
            _positionManager,
            config.StrategyConfigJson);

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
                _marketContextBuilder.UpdateIndicators(replayData.Candles15m[index]);
            }

            for (var index = replayData.WarmupEndIndex; index < replayData.Candles15m.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var candle = replayData.Candles15m[index];
                var fills = executionEngine.ProcessCandle(candle);

                foreach (var fill in fills)
                {
                    RecordFill(tradeLog, currentGridState, fill);
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
                if (TryCountClosedGridCycle(currentGridState, countedClosedCycles))
                {
                    gridCycles++;
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

            return metricsCalculator.Calculate(
                tradeLog,
                equityTimeSeries,
                config.InitialCapital,
                gridCycles,
                Math.Max(0, replayData.Candles15m.Count - replayData.WarmupEndIndex));
        }
        finally
        {
            _executionContextAccessor.CurrentExecutionEngine = null;
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
        var gridCycleId = gridState.GridCycleId ?? "default";
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

        var openTrade = tradeLog.FirstOrDefault(trade =>
            trade.ExitTimeUtc is null &&
            IsCompatibleExit(trade, fill.TradeType));

        if (openTrade is null)
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

        var closedSize = Math.Min(openTrade.Size, fill.Size);

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
            Fees = openTrade.Fees + fill.Fee,
            TradeType = openTrade.TradeType
        };

        tradeLog[tradeLog.IndexOf(openTrade)] = pairedTrade;
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
}