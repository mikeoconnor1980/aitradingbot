using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.TradeJournal.Models;

namespace TradePilot.Application.TradeJournal.Queries;

/// <summary>Requests deterministic strategy-version and recorded-regime comparisons.</summary>
public sealed record GetStrategyTradeAnalyticsQuery(
    string UserId,
    Guid StrategyId,
    string? Symbol = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : Query<StrategyTradeAnalytics>;

/// <summary>Delegates grouped trade analytics to persistence.</summary>
public sealed class GetStrategyTradeAnalyticsQueryHandler
    : QueryHandler<GetStrategyTradeAnalyticsQuery, StrategyTradeAnalytics>
{
    private readonly ITradeJournalRepository _repository;

    public GetStrategyTradeAnalyticsQueryHandler(ITradeJournalRepository repository)
    {
        _repository = repository;
    }

    public override Task<StrategyTradeAnalytics> Handle(
        GetStrategyTradeAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);
        return _repository.GetStrategyAnalyticsAsync(new TradeJournalFilter(
            request.UserId,
            request.StrategyId,
            Symbol: request.Symbol,
            FromUtc: request.From?.UtcDateTime,
            ToUtc: request.To?.UtcDateTime), cancellationToken);
    }
}
