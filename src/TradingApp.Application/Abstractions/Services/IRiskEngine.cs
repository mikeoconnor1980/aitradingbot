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
}
