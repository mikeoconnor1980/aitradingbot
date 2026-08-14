using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.TradeJournal.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.TradeJournal.Queries;

/// <summary>Requests deterministic aggregate facts for completed trades.</summary>
public sealed record GetTradeAnalyticsQuery(
    string UserId,
    Guid? StrategyId = null,
    int? StrategyVersion = null,
    string? Symbol = null,
    TradeSide? Side = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    TradeOutcome? Outcome = null) : Query<TradeAnalytics>;

/// <summary>Delegates completed-trade aggregation to persistence.</summary>
public sealed class GetTradeAnalyticsQueryHandler : QueryHandler<GetTradeAnalyticsQuery, TradeAnalytics>
{
    private readonly ITradeJournalRepository _repository;

    public GetTradeAnalyticsQueryHandler(ITradeJournalRepository repository)
    {
        _repository = repository;
    }

    public override Task<TradeAnalytics> Handle(GetTradeAnalyticsQuery request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);
        return _repository.GetAnalyticsAsync(new TradeJournalFilter(
            request.UserId,
            request.StrategyId,
            request.StrategyVersion,
            request.Symbol,
            request.Side,
            request.From?.UtcDateTime,
            request.To?.UtcDateTime,
            request.Outcome), cancellationToken);
    }
}
