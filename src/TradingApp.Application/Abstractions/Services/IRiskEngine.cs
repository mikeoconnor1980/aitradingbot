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
}
