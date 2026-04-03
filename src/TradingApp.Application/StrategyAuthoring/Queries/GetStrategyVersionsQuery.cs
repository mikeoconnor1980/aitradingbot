using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Identity;
using TradingApp.Application.Abstractions.Models;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.StrategyAuthoring.Queries;

public sealed record GetStrategyVersionsQuery(
    Guid StrategyId,
    int Page,
    int PageSize,
    AppIdentity Identity) : Query<PagedResult<StrategyRevisionSummaryDto>>;

public sealed class GetStrategyVersionsQueryHandler
    : QueryHandler<GetStrategyVersionsQuery, PagedResult<StrategyRevisionSummaryDto>>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyRevisionRepository _revisionRepository;

    public GetStrategyVersionsQueryHandler(
        IStrategyRepository strategyRepository,
        IStrategyRevisionRepository revisionRepository)
    {
        _strategyRepository = strategyRepository;
        _revisionRepository = revisionRepository;
    }

    public override async Task<PagedResult<StrategyRevisionSummaryDto>> Handle(
        GetStrategyVersionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Identity);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.PageSize, 100);

        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken);

        if (strategy is null || strategy.UserId != request.Identity.UserId || !strategy.IsActive)
        {
            throw new NotFoundException(nameof(Strategy), request.StrategyId);
        }

        var pagedRevisions = await _revisionRepository.GetPagedByStrategyIdAsync(
            request.StrategyId,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<StrategyRevisionSummaryDto>
        {
            Items = pagedRevisions.Items
                .Select(revision => new StrategyRevisionSummaryDto
                {
                    RevisionNumber = revision.RevisionNumber,
                    Source = revision.Source.ToString(),
                    Label = revision.Label,
                    ChangeSummary = revision.ChangeSummary,
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(revision.CreatedAtUtc).UtcDateTime,
                })
                .ToList(),
            Page = pagedRevisions.Page,
            PageSize = pagedRevisions.PageSize,
            TotalCount = pagedRevisions.TotalCount,
        };
    }
}