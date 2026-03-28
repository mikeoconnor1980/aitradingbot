using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Trading.Services;

public sealed class PassThroughRiskEngine : IRiskEngine
{
    public Task<IReadOnlyList<TradingSignal>> ValidateAsync(
        IReadOnlyList<TradingSignal> signals,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(signals);
        return Task.FromResult(signals);
    }
}