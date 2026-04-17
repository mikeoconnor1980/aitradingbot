using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Backtesting.Models;

namespace TradePilot.Api.Services;

public sealed class UnavailableBacktestRunner : IBacktestRunner
{
    public Task<BacktestResult> RunAsync(BacktestConfig config, CancellationToken cancellationToken = default)
    {
        throw new BacktestUnavailableException(
            "Backtest execution is not available in the API host yet. The concrete strategy pipeline services are not registered, so the endpoint can validate and read saved results but cannot run new backtests.");
    }

    public Task<BacktestResult> RunAsync(BacktestConfig config, Action<int, int, long>? onProgress, CancellationToken cancellationToken = default)
    {
        return RunAsync(config, cancellationToken);
    }

    public Task<BacktestResult> RunAsync(BacktestConfig config, ReplayData preloadedData, CancellationToken cancellationToken = default)
    {
        return RunAsync(config, cancellationToken);
    }
}