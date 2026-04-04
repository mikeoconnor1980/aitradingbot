namespace TradingApp.Application.Abstractions.Services;

public interface IStrategyReviewer
{
    Task<string> ReviewAsync(string strategyJson, CancellationToken cancellationToken);
}