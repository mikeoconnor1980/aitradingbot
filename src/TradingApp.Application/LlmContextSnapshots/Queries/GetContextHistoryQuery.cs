using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.LlmContextSnapshots.Models;

namespace TradingApp.Application.LlmContextSnapshots.Queries;

public sealed record GetContextHistoryQuery(string Symbol, long FromUtc, long ToUtc) : Query<IReadOnlyList<LlmContextDto>>;

public sealed class GetContextHistoryQueryHandler : QueryHandler<GetContextHistoryQuery, IReadOnlyList<LlmContextDto>>
{
    private readonly ILlmContextSnapshotRepository _repository;

    public GetContextHistoryQueryHandler(ILlmContextSnapshotRepository repository)
    {
        _repository = repository;
    }

    public override async Task<IReadOnlyList<LlmContextDto>> Handle(
        GetContextHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var snapshots = await _repository.GetHistoryAsync(
            request.Symbol,
            request.FromUtc,
            request.ToUtc,
            cancellationToken);

        return snapshots.Select(s => new LlmContextDto
        {
            Symbol = s.Symbol,
            MarketSentiment = s.MarketSentiment,
            MacroRegime = s.MacroRegime,
            EventRisk = s.EventRisk,
            Confidence = s.Confidence,
            DerivedRegime = s.DerivedRegime,
            Summary = s.Summary,
            GeneratedAtUtc = s.GeneratedAtUtc,
        }).ToList();
    }
}
