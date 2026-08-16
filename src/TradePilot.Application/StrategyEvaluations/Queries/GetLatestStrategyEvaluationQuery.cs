using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.StrategyEvaluations.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.StrategyEvaluations.Queries;

/// <summary>Requests the latest recorded evaluation matching a strategy and optional market filters.</summary>
public sealed record GetLatestStrategyEvaluationQuery(
    Guid? StrategyId = null,
    string? StrategyName = null,
    int? StrategyVersion = null,
    string? Symbol = null,
    DateTimeOffset? AtOrBefore = null) : Query<StrategyEvaluation?>;

/// <summary>Handles latest-evaluation retrieval without consulting current market state.</summary>
public sealed class GetLatestStrategyEvaluationQueryHandler
    : QueryHandler<GetLatestStrategyEvaluationQuery, StrategyEvaluation?>
{
    private readonly IStrategyEvaluationRepository _repository;

    public GetLatestStrategyEvaluationQueryHandler(IStrategyEvaluationRepository repository)
    {
        _repository = repository;
    }

    public override Task<StrategyEvaluation?> Handle(
        GetLatestStrategyEvaluationQuery request,
        CancellationToken cancellationToken)
    {
        GetStrategyEvaluationsQueryHandler.ValidateIdentity(request.StrategyId, request.StrategyName);
        return _repository.GetLatestAsync(
            new StrategyEvaluationFilter(
                request.StrategyId,
                request.StrategyName,
                request.StrategyVersion,
                request.Symbol,
                ToUtc: request.AtOrBefore?.ToUnixTimeMilliseconds()),
            cancellationToken);
    }
}
