using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.TradeJournal.Queries;

/// <summary>Requests full journal and linked strategy evidence for one owned trade.</summary>
public sealed record GetTradeQuery(Guid TradeId, string UserId) : Query<TradeJournalRecord?>;

/// <summary>Handles one owned trade retrieval.</summary>
public sealed class GetTradeQueryHandler : QueryHandler<GetTradeQuery, TradeJournalRecord?>
{
    private readonly ITradeJournalRepository _repository;

    public GetTradeQueryHandler(ITradeJournalRepository repository)
    {
        _repository = repository;
    }

    public override Task<TradeJournalRecord?> Handle(GetTradeQuery request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);
        return _repository.GetByIdAsync(request.TradeId, request.UserId, cancellationToken);
    }
}
