using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.Trading.Services;

/// <summary>
/// Production risk engine that validates signals against configurable limits:
/// daily loss circuit breaker, max open orders, max order size.
/// Replaces <see cref="PassThroughRiskEngine"/> for live trading.
/// </summary>
public sealed class LiveRiskEngine : IRiskEngine
{
    private readonly RiskLimitsConfig _limits;
    private readonly ILogger<LiveRiskEngine> _logger;
    private readonly IExecutionLogger _executionLogger;

    private readonly ConcurrentQueue<LossRecord> _recentLosses = new();
    private readonly ConcurrentDictionary<string, decimal> _positionRisks = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _circuitBreakerTripped;
    private volatile bool _drawdownCircuitBreakerTripped;
    private DateTimeOffset _circuitBreakerTrippedAt;
    private int _activeOrderCount;
    private decimal _accountEquity;
    private decimal _drawdownScalingFactor = 1.0m;
    private readonly object _lock = new();

    public LiveRiskEngine(
        IOptions<RiskLimitsConfig> limits,
        ILogger<LiveRiskEngine> logger,
        IExecutionLogger? executionLogger = null)
    {
        _limits = limits?.Value ?? throw new ArgumentNullException(nameof(limits));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _executionLogger = executionLogger ?? NullExecutionLogger.Instance;
    }

    /// <summary>Current active order count tracked by the engine.</summary>
    internal int ActiveOrderCount
    {
        get { lock (_lock) return _activeOrderCount; }
    }

    internal int TrackedPositionCount => _positionRisks.Count;

    internal decimal TrackedEquity
    {
        get { lock (_lock) return _accountEquity; }
    }

    /// <summary>Whether the circuit breaker is currently tripped.</summary>
    public bool IsCircuitBreakerTripped => _circuitBreakerTripped;

    public decimal DrawdownScalingFactor
    {
        get
        {
            lock (_lock)
            {
                return _drawdownScalingFactor;
            }
        }
    }

    public bool IsDrawdownCircuitBreakerTripped => _drawdownCircuitBreakerTripped;

    public Task<IReadOnlyList<TradingSignal>> ValidateAsync(
        IReadOnlyList<TradingSignal> signals,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(signals);

        if (signals.Count == 0)
        {
            return Task.FromResult(signals);
        }

        // Check circuit breaker auto-reset
        CheckCircuitBreakerReset();

        var approved = new List<TradingSignal>(signals.Count);

        foreach (var signal in signals)
        {
            // CancelGrid and TakeProfit always pass — they reduce risk
            if (IsRiskReducing(signal))
            {
                approved.Add(signal);
                TrackPositionCloseFromSignal(signal);
                continue;
            }

            if (_circuitBreakerTripped)
            {
                _logger.LogWarning(
                    "RISK: Signal BLOCKED by circuit breaker — Type={SignalType}, Symbol={Symbol}",
                    signal.SignalType, signal.Symbol);
                _executionLogger.LogDetail(
                    ExecutionLogCategory.RiskEngine,
                    $"Signal BLOCKED by circuit breaker: {signal.SignalType} {signal.Symbol}");
                continue;
            }

            if (_drawdownCircuitBreakerTripped)
            {
                _logger.LogWarning(
                    "RISK: Signal BLOCKED by drawdown circuit breaker — Type={SignalType}, Symbol={Symbol}",
                    signal.SignalType, signal.Symbol);
                _executionLogger.LogDetail(
                    ExecutionLogCategory.RiskEngine,
                    $"Signal BLOCKED by drawdown circuit breaker: {signal.SignalType} {signal.Symbol}");
                continue;
            }

            // Check max order size
            if (!CheckOrderSize(signal))
            {
                _executionLogger.LogDetail(
                    ExecutionLogCategory.RiskEngine,
                    $"Signal BLOCKED by max order size: {signal.SignalType} {signal.Symbol}");
                continue;
            }

            // Check max open orders
            if (!CheckOpenOrderLimit(signal))
            {
                _executionLogger.LogDetail(
                    ExecutionLogCategory.RiskEngine,
                    $"Signal BLOCKED by max open orders: {signal.SignalType} {signal.Symbol}");
                continue;
            }

            if (!CheckPortfolioHeat(signal))
            {
                _executionLogger.LogDetail(
                    ExecutionLogCategory.RiskEngine,
                    $"Signal BLOCKED by portfolio heat: {signal.SignalType} {signal.Symbol}");
                continue;
            }

            approved.Add(signal);
            TrackPositionOpenFromSignal(signal);
        }

        _logger.LogInformation(
            "RISK: {Approved}/{Total} signals approved. CircuitBreaker={CB}",
            approved.Count, signals.Count, _circuitBreakerTripped ? "TRIPPED" : "OK");

        return Task.FromResult<IReadOnlyList<TradingSignal>>(approved);
    }

    /// <summary>
    /// Record a realized loss. Called by the position manager after a losing trade closes.
    /// Trips the circuit breaker when rolling 24h losses exceed the daily limit.
    /// </summary>
    public void RecordLoss(decimal lossUsd)
    {
        if (lossUsd <= 0)
        {
            return;
        }

        _recentLosses.Enqueue(new LossRecord(DateTimeOffset.UtcNow, lossUsd));
        PruneOldLosses();

        var totalLoss = GetRollingDailyLoss();

        if (totalLoss >= _limits.MaxDailyLossUsd)
        {
            _circuitBreakerTripped = true;
            _circuitBreakerTrippedAt = DateTimeOffset.UtcNow;

            _logger.LogCritical(
                "RISK: Circuit breaker TRIPPED! Rolling 24h loss ${TotalLoss:N2} exceeds limit ${Limit:N2}. " +
                "All new orders blocked until cooldown ({Cooldown}min) or restart.",
                totalLoss, _limits.MaxDailyLossUsd, _limits.CircuitBreakerCooldownMinutes);
        }
    }

    /// <summary>
    /// Notify the engine that orders were placed (to track open order count).
    /// </summary>
    public void RecordOrdersPlaced(int count)
    {
        lock (_lock)
        {
            _activeOrderCount += count;
        }
    }

    /// <summary>
    /// Notify the engine that orders were filled or cancelled.
    /// </summary>
    public void RecordOrdersClosed(int count)
    {
        lock (_lock)
        {
            _activeOrderCount = Math.Max(0, _activeOrderCount - count);
        }
    }

    public void Reset()
    {
        while (_recentLosses.TryDequeue(out _))
        {
        }

        _positionRisks.Clear();
        _circuitBreakerTripped = false;
        _drawdownCircuitBreakerTripped = false;
        _circuitBreakerTrippedAt = default;

        lock (_lock)
        {
            _activeOrderCount = 0;
            _accountEquity = 0m;
            _drawdownScalingFactor = 1.0m;
        }

        _logger.LogInformation("RISK: Session state reset.");
    }

    /// <summary>Manually reset the circuit breaker (e.g., after operator review).</summary>
    public void ResetCircuitBreaker()
    {
        _circuitBreakerTripped = false;
        _logger.LogWarning("RISK: Circuit breaker manually reset.");
    }

    /// <summary>
    /// Update the engine's knowledge of current account equity.
    /// Called before validation so portfolio heat checks can be evaluated.
    /// </summary>
    public void UpdatePortfolioState(decimal accountEquity)
    {
        lock (_lock)
        {
            _accountEquity = Math.Max(0m, accountEquity);
        }
    }

    public void UpdateDrawdownState(decimal scalingFactor, bool isHalted)
    {
        var wasHalted = _drawdownCircuitBreakerTripped;

        lock (_lock)
        {
            _drawdownScalingFactor = Math.Max(0m, scalingFactor);
            _drawdownCircuitBreakerTripped = isHalted;
        }

        if (isHalted && !wasHalted)
        {
            _logger.LogCritical("RISK: Drawdown circuit breaker TRIPPED — all new entries halted.");
            return;
        }

        if (!isHalted && wasHalted)
        {
            _logger.LogWarning(
                "RISK: Drawdown circuit breaker RESET — trading resumed at scaling factor {ScalingFactor}.",
                DrawdownScalingFactor);
        }
    }

    /// <summary>
    /// Record that a position was opened with the given risk amount.
    /// </summary>
    public void RecordPositionOpened(string symbol, decimal riskUsd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        if (riskUsd <= 0m)
        {
            return;
        }

        _positionRisks[symbol] = riskUsd;
        _logger.LogInformation(
            "RISK: Position opened - Symbol={Symbol}, RiskUsd={RiskUsd:N2}, TrackedPositions={TrackedPositions}",
            symbol, riskUsd, _positionRisks.Count);
    }

    /// <summary>
    /// Record that a position was fully closed.
    /// </summary>
    public void RecordPositionClosed(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        if (_positionRisks.TryRemove(symbol, out var removedRisk))
        {
            _logger.LogInformation(
                "RISK: Position closed - Symbol={Symbol}, RemovedRiskUsd={RemovedRiskUsd:N2}, TrackedPositions={TrackedPositions}",
                symbol, removedRisk, _positionRisks.Count);
        }
    }

    internal decimal GetRollingDailyLoss()
    {
        PruneOldLosses();
        var total = 0m;
        foreach (var record in _recentLosses)
        {
            total += record.LossUsd;
        }

        return total;
    }

    private static bool IsRiskReducing(TradingSignal signal)
    {
        return signal.SignalType is "TakeProfit" or "CancelGrid" or "FlattenPosition" or "CloseHedge";
    }

    private bool CheckPortfolioHeat(TradingSignal signal)
    {
        decimal equity;
        lock (_lock)
        {
            equity = _accountEquity;
        }

        if (_limits.MaxPortfolioHeatPercent <= 0m || equity <= 0m)
        {
            return true;
        }

        if (!TryGetEstimatedRisk(signal, out var newTradeRiskUsd))
        {
            return true;
        }

        var currentHeatUsd = _positionRisks.Values.Sum();
        var maxHeatUsd = equity * (_limits.MaxPortfolioHeatPercent / 100m);

        if (currentHeatUsd + newTradeRiskUsd <= maxHeatUsd)
        {
            return true;
        }

        var currentHeatPct = PortfolioHeatCalculator.CalculateHeatPercent(_positionRisks.Values, equity);
        var newTradePct = (newTradeRiskUsd / equity) * 100m;

        _logger.LogWarning(
            "RISK: Signal BLOCKED by portfolio heat - CurrentHeat={CurrentHeatPct:N2}%, NewTrade={NewTradePct:N2}%, MaxHeat={MaxHeatPct:N2}%, Type={SignalType}, Symbol={Symbol}",
            currentHeatPct, newTradePct, _limits.MaxPortfolioHeatPercent, signal.SignalType, signal.Symbol);
        return false;
    }

    private bool CheckOrderSize(TradingSignal signal)
    {
        if (signal.Parameters is not null
            && signal.Parameters.TryGetValue("notionalUsd", out var notionalObj)
            && notionalObj is decimal notional
            && notional > _limits.MaxOrderSizeUsd)
        {
            _logger.LogWarning(
                "RISK: Signal BLOCKED — order size ${Notional:N2} exceeds max ${Max:N2}. Type={SignalType}, Symbol={Symbol}",
                notional, _limits.MaxOrderSizeUsd, signal.SignalType, signal.Symbol);
            return false;
        }

        return true;
    }

    private bool CheckOpenOrderLimit(TradingSignal signal)
    {
        if (signal.SignalType == "DeployGrid"
            && signal.Parameters is not null
            && signal.Parameters.TryGetValue("gridLevels", out var levelsObj)
            && levelsObj is int levels)
        {
            lock (_lock)
            {
                if (_activeOrderCount + levels > _limits.MaxOpenOrders)
                {
                    _logger.LogWarning(
                        "RISK: Signal BLOCKED — deploying {Levels} levels would exceed max open orders ({Current}/{Max}). Symbol={Symbol}",
                        levels, _activeOrderCount, _limits.MaxOpenOrders, signal.Symbol);
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryGetEstimatedRisk(TradingSignal signal, out decimal riskUsd)
    {
        riskUsd = 0m;

        if (signal.Parameters is null
            || !signal.Parameters.TryGetValue("estimatedRiskUsd", out var estimatedRisk))
        {
            return false;
        }

        try
        {
            riskUsd = Convert.ToDecimal(estimatedRisk);
            return riskUsd > 0m;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    private void TrackPositionOpenFromSignal(TradingSignal signal)
    {
        if (TryGetEstimatedRisk(signal, out var riskUsd))
        {
            RecordPositionOpened(signal.Symbol, riskUsd);
        }
    }

    private void TrackPositionCloseFromSignal(TradingSignal signal)
    {
        if (signal.SignalType is "FlattenPosition" or "CloseHedge")
        {
            RecordPositionClosed(signal.Symbol);
        }
    }

    private void CheckCircuitBreakerReset()
    {
        if (!_circuitBreakerTripped || _limits.CircuitBreakerCooldownMinutes <= 0)
        {
            return;
        }

        var elapsed = DateTimeOffset.UtcNow - _circuitBreakerTrippedAt;
        if (elapsed >= TimeSpan.FromMinutes(_limits.CircuitBreakerCooldownMinutes))
        {
            _circuitBreakerTripped = false;
            _logger.LogWarning(
                "RISK: Circuit breaker auto-reset after {Minutes}min cooldown.", _limits.CircuitBreakerCooldownMinutes);
        }
    }

    private void PruneOldLosses()
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        while (_recentLosses.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
        {
            _recentLosses.TryDequeue(out _);
        }
    }

    private readonly record struct LossRecord(DateTimeOffset Timestamp, decimal LossUsd);
}
