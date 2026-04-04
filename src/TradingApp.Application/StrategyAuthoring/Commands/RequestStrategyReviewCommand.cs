using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Identity;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.StrategyAuthoring.Commands;

public sealed record RequestStrategyReviewCommand(
    Guid StrategyId,
    int RevisionNumber,
    AppIdentity Identity) : Command<StrategyReviewDto>;

public sealed class RequestStrategyReviewCommandHandler
    : CommandHandler<RequestStrategyReviewCommand, StrategyReviewDto>
{
    private readonly IOptions<LlmReviewOptions> _options;
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyRevisionRepository _revisionRepository;
    private readonly IStrategyReviewRepository _reviewRepository;
    private readonly IBacktestRunRepository _backtestRunRepository;
    private readonly IStrategyReviewer _reviewer;

    public RequestStrategyReviewCommandHandler(
        IStrategyRepository strategyRepository,
        IStrategyRevisionRepository revisionRepository,
        IStrategyReviewRepository reviewRepository,
        IBacktestRunRepository backtestRunRepository,
        IStrategyReviewer reviewer,
        IOptions<LlmReviewOptions> options)
    {
        _strategyRepository = strategyRepository;
        _revisionRepository = revisionRepository;
        _reviewRepository = reviewRepository;
        _backtestRunRepository = backtestRunRepository;
        _reviewer = reviewer;
        _options = options;
    }

    public override async Task<StrategyReviewDto> Handle(
        RequestStrategyReviewCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Identity);

        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken);

        if (strategy is null || strategy.UserId != request.Identity.UserId || !strategy.IsActive)
        {
            throw new NotFoundException(nameof(Strategy), request.StrategyId);
        }

        var revision = await _revisionRepository.GetByStrategyAndRevisionAsync(
            request.StrategyId,
            request.RevisionNumber,
            cancellationToken)
            ?? throw new NotFoundException(
                $"Revision {request.RevisionNumber} not found for strategy {request.StrategyId}.");

        var latestBacktest = await _backtestRunRepository.GetLatestCompletedByStrategyRevisionAsync(
            request.StrategyId,
            request.RevisionNumber,
            cancellationToken);

        var backtestSummary = latestBacktest is not null
            ? BacktestSummaryForReview.FromBacktestRun(latestBacktest)
            : null;

        var result = await _reviewer.ReviewAsync(revision.ConfigJson, backtestSummary, cancellationToken);
        var review = StrategyReview.Create(
            request.StrategyId,
            request.RevisionNumber,
            result.ReviewMarkdown,
            _options.Value.ModelName,
            result.IsFallback);

        await _reviewRepository.DeleteByStrategyAndRevisionAsync(
            request.StrategyId,
            request.RevisionNumber,
            cancellationToken);

        await _reviewRepository.AddAsync(review, cancellationToken);

        return new StrategyReviewDto
        {
            Id = review.Id,
            StrategyId = review.StrategyId,
            RevisionNumber = review.RevisionNumber,
            ReviewMarkdown = review.ReviewMarkdown,
            ModelName = review.ModelName,
            IsFallback = review.IsFallback,
            CreatedAtUtc = review.CreatedAtUtc,
        };
    }
}