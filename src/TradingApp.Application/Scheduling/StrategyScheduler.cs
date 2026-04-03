using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.Scheduling.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Trading;

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
    private readonly ISignalController? _signalController;
    private readonly IBacktestAuditCollector _auditCollector;
    private readonly IStrategyConfig _strategyConfig;
    private readonly string _triggerTimeframe;
    private readonly decimal _initialCapital;
    private readonly BacktestExecutionContextAccessor? _executionContextAccessor;

    private GridState _gridState = new();
    private PositionState _positionState = new();

    public StrategyScheduler(
        IMarketContextBuilder contextBuilder,
        IStrategyEngine strategyEngine,
        IGridController gridController,
        IRiskEngine riskEngine,
        IPositionManager positionManager,
        IStrategyConfig strategyConfig,
        string triggerTimeframe = "15m",
        IBacktestAuditCollector? auditCollector = null,
        ISignalController? signalController = null,
        decimal initialCapital = 0m,
        BacktestExecutionContextAccessor? executionContextAccessor = null)
    {
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _strategyEngine = strategyEngine ?? throw new ArgumentNullException(nameof(strategyEngine));
        _gridController = gridController ?? throw new ArgumentNullException(nameof(gridController));
        _riskEngine = riskEngine ?? throw new ArgumentNullException(nameof(riskEngine));
        _positionManager = positionManager ?? throw new ArgumentNullException(nameof(positionManager));
        ArgumentNullException.ThrowIfNull(strategyConfig);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerTimeframe);

        _auditCollector = auditCollector ?? NullBacktestAuditCollector.Instance;
        _signalController = signalController;
        _strategyConfig = strategyConfig;
        _triggerTimeframe = triggerTimeframe;
        _initialCapital = initialCapital;
        _executionContextAccessor = executionContextAccessor;
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

        IReadOnlyList<IndicatorRequirement>? requiredIndicators = null;
        if (_strategyConfig is StrategyConfig typedConfig
            && typedConfig.StrategyMode == StrategyMode.Signal)
        {
            requiredIndicators = IndicatorExtractor.Extract(typedConfig);
        }

        var context = _contextBuilder.Build(
            evt.Candle,
            latestOneHourCandle,
            latestFourHourCandle,
            requiredIndicators);
        context.AccountEquity = ResolveAccountEquity();

        var evaluation = await _strategyEngine.EvaluateAsync(
            context,
            _strategyConfig,
            cancellationToken);

        var signals = await ProcessEvaluationAsync(
            evaluation,
            context,
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

    private decimal ResolveAccountEquity()
    {
        var accountEquity = _initialCapital;

        var simulatedPosition = _executionContextAccessor?.CurrentExecutionEngine?.GetPosition();
        if (simulatedPosition is not null)
        {
            accountEquity += simulatedPosition.RealisedPnL + simulatedPosition.UnrealisedPnL;
            return Math.Max(0m, accountEquity);
        }

        if (_positionState.IsOpen)
        {
            accountEquity += _positionState.UnrealisedPnL;
        }

        return Math.Max(0m, accountEquity);
    }

    private Task<IReadOnlyList<TradingSignal>> ProcessEvaluationAsync(
        StrategyEvaluation evaluation,
        MarketContext context,
        CancellationToken cancellationToken)
    {
        if (_signalController is not null
            && _strategyConfig is StrategyConfig { StrategyMode: StrategyMode.Signal })
        {
            return _signalController.ProcessAsync(
                evaluation,
                context,
                _positionState,
                _strategyConfig,
                cancellationToken);
        }

        return _gridController.ProcessAsync(
            evaluation,
            context,
            _gridState,
            _positionState,
            _strategyConfig,
            cancellationToken);
    }
}