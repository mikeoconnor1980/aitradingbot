using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.StrategyEvaluations.Models;

namespace TradePilot.Application.StrategyEvaluations.Queries;

/// <summary>Requests a bounded, newest-first strategy-evaluation history.</summary>
public sealed record GetStrategyEvaluationsQuery(
    Guid? StrategyId = null,
    string? StrategyName = null,
    int? StrategyVersion = null,
    string? Symbol = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Limit = 100) : Query<StrategyEvaluationsResult>;

/// <summary>Handles bounded strategy-evaluation history retrieval.</summary>
public sealed class GetStrategyEvaluationsQueryHandler
    : QueryHandler<GetStrategyEvaluationsQuery, StrategyEvaluationsResult>
{
    public const int MaximumLimit = 500;
    private readonly IStrategyEvaluationRepository _repository;

    public GetStrategyEvaluationsQueryHandler(IStrategyEvaluationRepository repository)
    {
        _repository = repository;
    }

    public override async Task<StrategyEvaluationsResult> Handle(
        GetStrategyEvaluationsQuery request,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(request.StrategyId, request.StrategyName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Limit);
        var limit = Math.Min(request.Limit, MaximumLimit);
        var evaluations = await _repository.GetAsync(CreateFilter(request), limit, cancellationToken);
        return new StrategyEvaluationsResult(evaluations, limit);
    }

    internal static StrategyEvaluationFilter CreateFilter(GetStrategyEvaluationsQuery request)
    {
        return new StrategyEvaluationFilter(
            request.StrategyId,
            request.StrategyName,
            request.StrategyVersion,
            request.Symbol,
            request.From?.ToUnixTimeMilliseconds(),
            request.To?.ToUnixTimeMilliseconds());
    }

    internal static void ValidateIdentity(Guid? strategyId, string? strategyName)
    {
        if (!strategyId.HasValue && string.IsNullOrWhiteSpace(strategyName))
        {
            throw new ArgumentException("A strategy ID or strategy name is required.", nameof(strategyId));
        }
    }
}
