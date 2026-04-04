namespace TradingApp.Application.Abstractions.Services;

public sealed record StrategyReviewResult(string ReviewMarkdown, bool IsFallback);

public interface IStrategyReviewer
{
    Task<StrategyReviewResult> ReviewAsync(string strategyJson, CancellationToken cancellationToken);
}