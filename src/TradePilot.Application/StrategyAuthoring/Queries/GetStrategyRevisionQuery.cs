using System.Text.Json;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.StrategyAuthoring.Queries;

public sealed record GetStrategyRevisionQuery(
    Guid StrategyId,
    int RevisionNumber,
    AppIdentity Identity) : Query<StrategyRevisionDto>;

public sealed class GetStrategyRevisionQueryHandler
    : QueryHandler<GetStrategyRevisionQuery, StrategyRevisionDto>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyRevisionRepository _revisionRepository;

    public GetStrategyRevisionQueryHandler(
        IStrategyRepository strategyRepository,
        IStrategyRevisionRepository revisionRepository)
    {
        _strategyRepository = strategyRepository;
        _revisionRepository = revisionRepository;
    }

    public override async Task<StrategyRevisionDto> Handle(
        GetStrategyRevisionQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Identity);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.RevisionNumber, 1);

        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken);

        if (strategy is null || strategy.UserId != request.Identity.UserId || !strategy.IsActive)
        {
            throw new NotFoundException(nameof(Strategy), request.StrategyId);
        }

        var revision = await _revisionRepository.GetByStrategyAndRevisionAsync(
            request.StrategyId,
            request.RevisionNumber,
            cancellationToken);

        if (revision is null)
        {
            throw new NotFoundException(nameof(StrategyRevision), request.RevisionNumber);
        }

        var config = JsonSerializer.Deserialize<StrategyConfig>(revision.ConfigJson, StrategyJsonOptions.Default)
            ?? new StrategyConfig();

        return new StrategyRevisionDto
        {
            RevisionNumber = revision.RevisionNumber,
            Source = revision.Source.ToString(),
            Label = revision.Label,
            ChangeSummary = revision.ChangeSummary,
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(revision.CreatedAtUtc).UtcDateTime,
            Config = config,
        };
    }
}