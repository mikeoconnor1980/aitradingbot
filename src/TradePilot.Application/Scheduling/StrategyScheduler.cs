using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.Backtesting;
using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.Backtesting.Services;
using TradePilot.Application.Scheduling.Models;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Scheduling;

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
    private readonly IDcaController? _dcaController;
    private readonly IBacktestAuditCollector _auditCollector;
    private readonly IExecutionLogger _executionLogger;
    private readonly IStrategyConfig _strategyConfig;
    private readonly string _triggerTimeframe;
    private readonly decimal _initialCapital;
    private readonly BacktestExecutionContextAccessor? _executionContextAccessor;
    private readonly IReadOnlyList<DrawdownTier> _drawdownTiers;
    private readonly IStrategyRepository? _strategyRepository;
    private readonly Strategy? _strategy;

    private readonly GridState _gridState;
    private PositionState _positionState = new();
    private MarketContext? _lastContext;
    private decimal? _highWaterMarkUsd;

    public StrategyScheduler(
        IMarketContextBuilder contextBuilder,
        IStrategyEngine strategyEngine,
        IGridController gridController,
        IRiskEngine riskEngine,
        IPositionManager positionManager,
        IStrategyConfig strategyConfig,
        string triggerTimeframe = "15m",
        IBacktestAuditCollector? auditCollector = null,
        IExecutionLogger? executionLogger = null,
        ISignalController? signalController = null,
        IDcaController? dcaController = null,
        decimal initialCapital = 0m,
        BacktestExecutionContextAccessor? executionContextAccessor = null,
        GridState? gridState = null,
        IReadOnlyList<DrawdownTier>? drawdownTiers = null,
        Strategy? strategy = null,
        IStrategyRepository? strategyRepository = null)
    {
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _strategyEngine = strategyEngine ?? throw new ArgumentNullException(nameof(strategyEngine));
        _gridController = gridController ?? throw new ArgumentNullException(nameof(gridController));
        _riskEngine = riskEngine ?? throw new ArgumentNullException(nameof(riskEngine));
        _positionManager = positionManager ?? throw new ArgumentNullException(nameof(positionManager));
        ArgumentNullException.ThrowIfNull(strategyConfig);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerTimeframe);

        _auditCollector = auditCollector ?? NullBacktestAuditCollector.Instance;
        _executionLogger = executionLogger ?? NullExecutionLogger.Instance;
        _signalController = signalController;
        _dcaController = dcaController;
        _strategyConfig = strategyConfig;
        _triggerTimeframe = triggerTimeframe;
        _initialCapital = initialCapital;
        _executionContextAccessor = executionContextAccessor;
        _gridState = gridState ?? new GridState();
        _drawdownTiers = drawdownTiers ?? [];
        _strategy = strategy;
        _strategyRepository = strategyRepository;
        _highWaterMarkUsd = strategy?.HighWaterMarkUsd;
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

        var context = await _contextBuilder.BuildAsync(
            evt.Candle,
            latestOneHourCandle,
            latestFourHourCandle,
            requiredIndicators,
            cancellationToken);
        context.AccountEquity = ResolveAccountEquity();
        _riskEngine.UpdatePortfolioState(context.AccountEquity);
        await ApplyDrawdownStateAsync(context, cancellationToken);
        _lastContext = context;

        _executionLogger.LogSummary(
            ExecutionLogCategory.CandleClose,
            $"Candle closed: {context.Symbol} {_triggerTimeframe} O={evt.Candle.Open:F2} H={evt.Candle.High:F2} L={evt.Candle.Low:F2} C={evt.Candle.Close:F2} V={evt.Candle.Volume:F0}");

        _executionLogger.LogDetail(
            ExecutionLogCategory.Indicator,
            $"Indicators: EMA9={context.Indicators?.EmaFast:F2} EMA21={context.Indicators?.EmaSlow:F2} EMATrend={context.Indicators?.EmaTrend:F2} RSI={context.Indicators?.Rsi:F2} ATR={context.Indicators?.Atr:F4}",
            new Dictionary<string, object>
            {
                ["emaFast"] = context.Indicators?.EmaFast ?? 0m,
                ["emaSlow"] = context.Indicators?.EmaSlow ?? 0m,
                ["emaTrend"] = context.Indicators?.EmaTrend ?? 0m,
                ["rsi"] = context.Indicators?.Rsi ?? 0m,
                ["atr"] = context.Indicators?.Atr ?? 0m,
            });

        _executionLogger.LogDetail(
            ExecutionLogCategory.Drawdown,
            $"Drawdown: Equity={context.AccountEquity:F2} ScalingFactor={context.DrawdownScalingFactor:F2}",
            new Dictionary<string, object>
            {
                ["equity"] = context.AccountEquity,
                ["scalingFactor"] = context.DrawdownScalingFactor,
                ["hwm"] = _highWaterMarkUsd ?? 0m,
            });

        var evaluation = await _strategyEngine.EvaluateAsync(
            context,
            _strategyConfig,
            cancellationToken);

        _executionLogger.LogSummary(
            ExecutionLogCategory.EntryGate,
            evaluation.SetupDetected
                ? $"Setup detected: {evaluation.Reason}"
                : $"No setup: {evaluation.Reason}",
            new Dictionary<string, object>
            {
                ["setupDetected"] = evaluation.SetupDetected,
                ["regime"] = evaluation.Regime?.ToString() ?? "Unknown",
            });

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
            GridCycleId = _gridState.GridCycleId,
            Regime = evaluation.Regime
        });

        if (signals.Count == 0)
        {
            _executionLogger.LogSummary(
                ExecutionLogCategory.Signal,
                $"No signals emitted. Grid={_gridState.Lifecycle}, Position={(_positionState.IsOpen ? $"open ({_positionState.Size:F4})" : "flat")}");
            return;
        }

        _executionLogger.LogSummary(
            ExecutionLogCategory.Signal,
            $"{signals.Count} signal(s) emitted: {string.Join(", ", signals.Select(s => s.SignalType))}");

        var approvedSignals = await _riskEngine.ValidateAsync(signals, cancellationToken);
        if (approvedSignals.Count == 0)
        {
            _executionLogger.LogSummary(
                ExecutionLogCategory.RiskEngine,
                "All signals REJECTED by risk engine.");
            return;
        }

        _executionLogger.LogSummary(
            ExecutionLogCategory.RiskEngine,
            $"Risk engine approved {approvedSignals.Count}/{signals.Count} signal(s).");

        await _positionManager.ExecuteSignalsAsync(approvedSignals, cancellationToken);

        _executionLogger.LogSummary(
            ExecutionLogCategory.Signal,
            $"Execution complete: {approvedSignals.Count} signal(s) sent to position manager.");
    }

    public void UpdateState(GridState gridState, PositionState positionState)
    {
        // GridState is shared by reference; only copy values if a different instance is passed
        if (gridState is not null && !ReferenceEquals(gridState, _gridState))
        {
            _gridState.Lifecycle = gridState.Lifecycle;
            _gridState.GridCycleId = gridState.GridCycleId;
            _gridState.FilledLevels = gridState.FilledLevels;
            _gridState.TotalLevels = gridState.TotalLevels;
            _gridState.TrailingStopHighWatermark = gridState.TrailingStopHighWatermark;
            _gridState.CandlesSinceEntry = gridState.CandlesSinceEntry;
        }

        _positionState = positionState ?? throw new ArgumentNullException(nameof(positionState));
    }

    public GridState GetGridState() => _gridState;

    /// <summary>
    /// Returns the most recent <see cref="MarketContext"/> built during candle evaluation.
    /// May be null before the first candle is processed.
    /// </summary>
    public MarketContext? LastContext => _lastContext;

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
                _gridState,
                _positionState,
                _strategyConfig,
                cancellationToken);
        }

        if (_dcaController is not null
            && _strategyConfig is StrategyConfig { StrategyMode: StrategyMode.Dca })
        {
            return _dcaController.ProcessAsync(
                evaluation,
                context,
                _gridState,
                _positionState,
                _strategyConfig,
                cancellationToken);
        }

        if (_strategyConfig is StrategyConfig { StrategyMode: StrategyMode.Dca })
        {
            return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
        }

        return _gridController.ProcessAsync(
            evaluation,
            context,
            _gridState,
            _positionState,
            _strategyConfig,
            cancellationToken);
    }

    private async Task ApplyDrawdownStateAsync(MarketContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var currentHighWaterMark = _highWaterMarkUsd
            ?? _strategy?.HighWaterMarkUsd
            ?? context.AccountEquity;
        var drawdownResult = DrawdownEvaluator.Evaluate(
            context.AccountEquity,
            currentHighWaterMark,
            _drawdownTiers);

        _highWaterMarkUsd = drawdownResult.NewHighWaterMark;
        context.DrawdownScalingFactor = drawdownResult.ScalingFactor;
        _riskEngine.UpdateDrawdownState(drawdownResult.ScalingFactor, drawdownResult.IsHalted);

        if (_strategy is null || drawdownResult.NewHighWaterMark == currentHighWaterMark)
        {
            return;
        }

        _strategy.UpdateHighWaterMark(drawdownResult.NewHighWaterMark);

        if (_strategyRepository is not null)
        {
            await _strategyRepository.UpdateAsync(_strategy, cancellationToken);
        }
    }
}