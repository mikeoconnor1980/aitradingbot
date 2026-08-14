using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.TradeJournal.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.TradeJournal.Queries;

/// <summary>Requests bounded logical trade history.</summary>
public sealed record GetTradesQuery(
    string UserId,
    Guid? StrategyId = null,
    int? StrategyVersion = null,
    string? Symbol = null,
    TradeSide? Side = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    TradeOutcome? Outcome = null,
    int Limit = 50) : Query<TradesResult>;

/// <summary>Handles bounded logical trade history retrieval.</summary>
public sealed class GetTradesQueryHandler : QueryHandler<GetTradesQuery, TradesResult>
{
    public const int MaximumLimit = 500;
    private readonly ITradeJournalRepository _repository;

    public GetTradesQueryHandler(ITradeJournalRepository repository)
    {
        _repository = repository;
    }

    public override async Task<TradesResult> Handle(GetTradesQuery request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Limit);
        var limit = Math.Min(request.Limit, MaximumLimit);
        var trades = await _repository.GetAsync(CreateFilter(request), limit, cancellationToken);
        return new TradesResult(trades, limit);
    }

    internal static TradeJournalFilter CreateFilter(GetTradesQuery request) => new(
        request.UserId,
        request.StrategyId,
        request.StrategyVersion,
        request.Symbol,
        request.Side,
        request.From?.UtcDateTime,
        request.To?.UtcDateTime,
        request.Outcome);
}
