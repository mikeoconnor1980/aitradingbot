using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Identity;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Services;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.StrategyAuthoring.Queries;

public sealed record GetStrategyDiffQuery(
    Guid StrategyId,
    int FromRevision,
    int ToRevision,
    AppIdentity Identity) : Query<StrategyDiffDto>;

public sealed class GetStrategyDiffQueryHandler
    : QueryHandler<GetStrategyDiffQuery, StrategyDiffDto>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyRevisionRepository _revisionRepository;
    private readonly IStrategyDiffService _diffService;

    public GetStrategyDiffQueryHandler(
        IStrategyRepository strategyRepository,
        IStrategyRevisionRepository revisionRepository,
        IStrategyDiffService diffService)
    {
        _strategyRepository = strategyRepository;
        _revisionRepository = revisionRepository;
        _diffService = diffService;
    }

    public override async Task<StrategyDiffDto> Handle(
        GetStrategyDiffQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Identity);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.FromRevision, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.ToRevision, 1);

        if (request.FromRevision == request.ToRevision)
        {
            throw new DomainException("Cannot diff a revision with itself");
        }

        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken);

        if (strategy is null || strategy.UserId != request.Identity.UserId || !strategy.IsActive)
        {
            throw new NotFoundException(nameof(Strategy), request.StrategyId);
        }

        var fromRevision = await _revisionRepository.GetByStrategyAndRevisionAsync(
            request.StrategyId,
            request.FromRevision,
            cancellationToken);

        if (fromRevision is null)
        {
            throw new NotFoundException(nameof(StrategyRevision), request.FromRevision);
        }

        var toRevision = await _revisionRepository.GetByStrategyAndRevisionAsync(
            request.StrategyId,
            request.ToRevision,
            cancellationToken);

        if (toRevision is null)
        {
            throw new NotFoundException(nameof(StrategyRevision), request.ToRevision);
        }

        return new StrategyDiffDto
        {
            FromRevision = request.FromRevision,
            ToRevision = request.ToRevision,
            Changes = _diffService.ComputeDiff(fromRevision.ConfigJson, toRevision.ConfigJson),
        };
    }
}