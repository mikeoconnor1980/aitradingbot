using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.StrategyEvaluations.Models;

namespace TradePilot.Application.StrategyEvaluations.Queries;

/// <summary>Requests database-calculated strategy-evaluation counts for a bounded range.</summary>
public sealed record GetStrategyEvaluationSummaryQuery(
    Guid? StrategyId = null,
    string? StrategyName = null,
    int? StrategyVersion = null,
    string? Symbol = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : Query<StrategyEvaluationSummary>;

/// <summary>Handles deterministic strategy-evaluation aggregation.</summary>
public sealed class GetStrategyEvaluationSummaryQueryHandler
    : QueryHandler<GetStrategyEvaluationSummaryQuery, StrategyEvaluationSummary>
{
    private readonly IStrategyEvaluationRepository _repository;

    public GetStrategyEvaluationSummaryQueryHandler(IStrategyEvaluationRepository repository)
    {
        _repository = repository;
    }

    public override Task<StrategyEvaluationSummary> Handle(
        GetStrategyEvaluationSummaryQuery request,
        CancellationToken cancellationToken)
    {
        GetStrategyEvaluationsQueryHandler.ValidateIdentity(request.StrategyId, request.StrategyName);
        return _repository.GetSummaryAsync(
            new StrategyEvaluationFilter(
                request.StrategyId,
                request.StrategyName,
                request.StrategyVersion,
                request.Symbol,
                request.From?.ToUnixTimeMilliseconds(),
                request.To?.ToUnixTimeMilliseconds()),
            cancellationToken);
    }
}
