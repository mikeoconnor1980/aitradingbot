using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Validates trading signals against risk limits.
/// </summary>
public interface IRiskEngine
{
    Task<IReadOnlyList<TradingSignal>> ValidateAsync(
        IReadOnlyList<TradingSignal> signals,
        CancellationToken cancellationToken = default);

    /// <summary>Validates signals and returns deterministic evidence for each risk decision.</summary>
    async Task<RiskValidationResult> ValidateWithEvidenceAsync(
        IReadOnlyList<TradingSignal> signals,
        CancellationToken cancellationToken = default)
    {
        var approvedSignals = await ValidateAsync(signals, cancellationToken);
        var passed = approvedSignals.Count == signals.Count;
        return new RiskValidationResult(
            approvedSignals,
            signals.Count == 0
                ? []
                :
                [
                    new RuleEvaluationResult(
                        "risk.validation",
                        "Risk validation",
                        TradePilot.Domain.Enums.RuleCategory.Risk,
                        passed,
                        passed
                            ? $"Risk engine approved all {signals.Count} signal(s)."
                            : $"Risk engine approved {approvedSignals.Count} of {signals.Count} signal(s).",
                        !passed,
                        TradePilot.Domain.Enums.RuleEvaluationKind.RiskOverride,
                        ActualValue: approvedSignals.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ActualNumericValue: approvedSignals.Count,
                        ExpectedValue: signals.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ExpectedNumericValue: signals.Count)
                ]);
    }

    /// <summary>Record a realized loss for the circuit breaker.</summary>
    void RecordLoss(decimal lossUsd) { }

    /// <summary>Notify the engine that orders were placed.</summary>
    void RecordOrdersPlaced(int count) { }

    /// <summary>Notify the engine that orders were filled or cancelled.</summary>
    void RecordOrdersClosed(int count) { }

    /// <summary>Reset all session-scoped risk state.</summary>
    void Reset() { }

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
