namespace TradePilot.Application.Backtesting.Experiments;

public interface IBacktestExperimentService
{
    Task<BacktestExperimentResult> RunAsync(
        BacktestExperimentRequest request,
        CancellationToken cancellationToken = default);
}