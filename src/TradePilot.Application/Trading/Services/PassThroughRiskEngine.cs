using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.Trading.Services;

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