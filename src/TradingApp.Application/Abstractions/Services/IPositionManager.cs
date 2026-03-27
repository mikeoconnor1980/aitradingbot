using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Executes approved trading signals through the execution engine.
/// </summary>
public interface IPositionManager
{
    Task ExecuteSignalsAsync(
        IReadOnlyList<TradingSignal> approvedSignals,
        CancellationToken cancellationToken = default);
}
