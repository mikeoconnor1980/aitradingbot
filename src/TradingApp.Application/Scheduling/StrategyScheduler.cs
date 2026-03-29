using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.Scheduling.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Scheduling;

/// <summary>
/// Subscribes to candle close events and orchestrates strategy evaluation
/// on the configured trigger timeframe.
/// </summary>
public sealed class StrategyScheduler
{
    private readonly IMarketContextBuilder _contextBuilder;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IGridController _gridController;
    private readonly IRiskEngine _riskEngine;
    private readonly IPositionManager _positionManager;
    private readonly IBacktestAuditCollector _auditCollector;
    private readonly string _strategyConfigJson;
    private readonly string _triggerTimeframe;

    private GridState _gridState = new();
    private PositionState _positionState = new();

    public StrategyScheduler(
        IMarketContextBuilder contextBuilder,
        IStrategyEngine strategyEngine,
        IGridController gridController,
        IRiskEngine riskEngine,
        IPositionManager positionManager,
        string strategyConfigJson,
        string triggerTimeframe = "15m",
        IBacktestAuditCollector? auditCollector = null)
    {
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _strategyEngine = strategyEngine ?? throw new ArgumentNullException(nameof(strategyEngine));
        _gridController = gridController ?? throw new ArgumentNullException(nameof(gridController));
        _riskEngine = riskEngine ?? throw new ArgumentNullException(nameof(riskEngine));
        _positionManager = positionManager ?? throw new ArgumentNullException(nameof(positionManager));
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyConfigJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerTimeframe);

        _auditCollector = auditCollector ?? NullBacktestAuditCollector.Instance;
        _strategyConfigJson = strategyConfigJson;
        _triggerTimeframe = triggerTimeframe;
    }

    public async Task HandleCandleClosedAsync(
        CandleClosedEvent evt,
        Candle? latestOneHourCandle,
        Candle? latestFourHourCandle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (!string.Equals(evt.Timeframe, _triggerTimeframe, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var context = _contextBuilder.Build(
            evt.Candle,
            latestOneHourCandle,
            latestFourHourCandle);

        var evaluation = await _strategyEngine.EvaluateAsync(
            context,
            _strategyConfigJson,
            cancellationToken);

        var signals = await _gridController.ProcessAsync(
            evaluation,
            context,
            _gridState,
            _positionState,
            _strategyConfigJson,
            cancellationToken);

        _auditCollector.LogCandleEvaluation(new CandleEvaluationEntry
        {
            TimestampUtc = evt.Candle.Timestamp,
            Open = evt.Candle.Open,
            High = evt.Candle.High,
            Low = evt.Candle.Low,
            Close = evt.Candle.Close,
            Volume = evt.Candle.Volume,
            IsWarmup = false,
            EmaFast = context.Indicators?.EmaFast ?? 0m,
            EmaSlow = context.Indicators?.EmaSlow ?? 0m,
            EmaTrend = context.Indicators?.EmaTrend ?? 0m,
            Rsi = context.Indicators?.Rsi ?? 0m,
            Atr = context.Indicators?.Atr ?? 0m,
            SetupDetected = evaluation.SetupDetected,
            GridLifecycleState = _gridState.Lifecycle.ToString(),
            PositionSize = _positionState.Size,
            PositionAvgEntry = _positionState.AverageEntryPrice,
            SignalsEmitted = signals.Select(signal => signal.SignalType).ToList(),
            GridCycleId = _gridState.GridCycleId
        });

        if (signals.Count == 0)
        {
            return;
        }

        var approvedSignals = await _riskEngine.ValidateAsync(signals, cancellationToken);
        if (approvedSignals.Count == 0)
        {
            return;
        }

        await _positionManager.ExecuteSignalsAsync(approvedSignals, cancellationToken);
    }

    public void UpdateState(GridState gridState, PositionState positionState)
    {
        _gridState = gridState ?? throw new ArgumentNullException(nameof(gridState));
        _positionState = positionState ?? throw new ArgumentNullException(nameof(positionState));
    }

    public GridState GetGridState() => _gridState;
}