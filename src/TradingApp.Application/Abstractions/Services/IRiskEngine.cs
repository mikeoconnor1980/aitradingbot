using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Validates trading signals against risk limits.
/// </summary>
public interface IRiskEngine
{
    Task<IReadOnlyList<TradingSignal>> ValidateAsync(
        IReadOnlyList<TradingSignal> signals,
        CancellationToken cancellationToken = default);

    /// <summary>Record a realized loss for the circuit breaker.</summary>
    void RecordLoss(decimal lossUsd) { }

    /// <summary>Notify the engine that orders were placed.</summary>
    void RecordOrdersPlaced(int count) { }

    /// <summary>Notify the engine that orders were filled or cancelled.</summary>
    void RecordOrdersClosed(int count) { }

    /// <summary>Update the engine's knowledge of current account equity.</summary>
    void UpdatePortfolioState(decimal accountEquity) { }

    /// <summary>Updates the drawdown state computed by the scheduler from equity vs HWM.</summary>
    void UpdateDrawdownState(decimal scalingFactor, bool isHalted) { }

    /// <summary>Current drawdown scaling factor (1.0 = full risk, 0.0 = halted).</summary>
    decimal DrawdownScalingFactor => 1.0m;

    /// <summary>Whether the drawdown circuit breaker is currently active.</summary>
    bool IsDrawdownCircuitBreakerTripped => false;

    /// <summary>Record that a position was opened with the given risk amount.</summary>
    void RecordPositionOpened(string symbol, decimal riskUsd) { }

    /// <summary>Record that a position was fully closed.</summary>
    void RecordPositionClosed(string symbol) { }
}
