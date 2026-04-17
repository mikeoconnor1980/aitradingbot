using TradePilot.Application.Backtesting.Models;

namespace TradePilot.Application.Abstractions.Services;

public sealed record StrategyReviewResult(string ReviewMarkdown, bool IsFallback);

public interface IStrategyReviewer
{
    Task<StrategyReviewResult> ReviewAsync(
        string strategyJson,
        BacktestSummaryForReview? backtestSummary,
        CancellationToken cancellationToken);
}