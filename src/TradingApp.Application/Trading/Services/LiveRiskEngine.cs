using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Trading.Services;

/// <summary>
/// Production risk engine that validates signals against configurable limits:
/// daily loss circuit breaker, max open orders, max order size.
/// Replaces <see cref="PassThroughRiskEngine"/> for live trading.
/// </summary>
public sealed class LiveRiskEngine : IRiskEngine
{
    private readonly RiskLimitsConfig _limits;
    private readonly ILogger<LiveRiskEngine> _logger;

    private readonly ConcurrentQueue<LossRecord> _recentLosses = new();
    private volatile bool _circuitBreakerTripped;
    private DateTimeOffset _circuitBreakerTrippedAt;
    private int _activeOrderCount;
    private readonly object _lock = new();

    public LiveRiskEngine(
        IOptions<RiskLimitsConfig> limits,
        ILogger<LiveRiskEngine> logger)
    {
        _limits = limits?.Value ?? throw new ArgumentNullException(nameof(limits));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Current active order count tracked by the engine.</summary>
    internal int ActiveOrderCount
    {
        get { lock (_lock) return _activeOrderCount; }
    }

    /// <summary>Whether the circuit breaker is currently tripped.</summary>
    public bool IsCircuitBreakerTripped => _circuitBreakerTripped;

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
                continue;
            }

            if (_circuitBreakerTripped)
            {
                _logger.LogWarning(
                    "RISK: Signal BLOCKED by circuit breaker — Type={SignalType}, Symbol={Symbol}",
                    signal.SignalType, signal.Symbol);
                continue;
            }

            // Check max order size
            if (!CheckOrderSize(signal))
            {
                continue;
            }

            // Check max open orders
            if (!CheckOpenOrderLimit(signal))
            {
                continue;
            }

            approved.Add(signal);
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

    /// <summary>Manually reset the circuit breaker (e.g., after operator review).</summary>
    public void ResetCircuitBreaker()
    {
        _circuitBreakerTripped = false;
        _logger.LogWarning("RISK: Circuit breaker manually reset.");
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
            && signal.Parameters.TryGetValue("levels", out var levelsObj)
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
