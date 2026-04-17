using TradePilot.Application.Backtesting.Models;

namespace TradePilot.Application.Abstractions.Services;

public interface IBacktestRunner
{
    Task<BacktestResult> RunAsync(
        BacktestConfig config,
        CancellationToken cancellationToken = default);

    Task<BacktestResult> RunAsync(
        BacktestConfig config,
        Action<int, int, long>? onProgress,
        CancellationToken cancellationToken = default);

    Task<BacktestResult> RunAsync(
        BacktestConfig config,
        ReplayData preloadedData,
        CancellationToken cancellationToken = default);
}
