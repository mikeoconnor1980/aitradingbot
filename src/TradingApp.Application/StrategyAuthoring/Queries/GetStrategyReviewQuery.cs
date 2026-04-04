using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Identity;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.StrategyAuthoring.Queries;

public sealed record GetStrategyReviewQuery(
    Guid StrategyId,
    int RevisionNumber,
    AppIdentity Identity) : Query<StrategyReviewDto?>;

public sealed class GetStrategyReviewQueryHandler
    : QueryHandler<GetStrategyReviewQuery, StrategyReviewDto?>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyReviewRepository _reviewRepository;

    public GetStrategyReviewQueryHandler(
        IStrategyRepository strategyRepository,
        IStrategyReviewRepository reviewRepository)
    {
        _strategyRepository = strategyRepository;
        _reviewRepository = reviewRepository;
    }

    public override async Task<StrategyReviewDto?> Handle(
        GetStrategyReviewQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Identity);

        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken);

        if (strategy is null || strategy.UserId != request.Identity.UserId || !strategy.IsActive)
        {
            throw new NotFoundException(nameof(Strategy), request.StrategyId);
        }

        var review = await _reviewRepository.GetByStrategyAndRevisionAsync(
            request.StrategyId,
            request.RevisionNumber,
            cancellationToken);

        if (review is null)
        {
            return null;
        }

        return new StrategyReviewDto
        {
            Id = review.Id,
            StrategyId = review.StrategyId,
            RevisionNumber = review.RevisionNumber,
            ReviewMarkdown = review.ReviewMarkdown,
            ModelName = review.ModelName,
            CreatedAtUtc = review.CreatedAtUtc,
        };
    }
}