using TradingApp.Application.Backtesting.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface IBacktestRunner
{
    Task<BacktestResult> RunAsync(BacktestConfig config, CancellationToken cancellationToken = default);
}
