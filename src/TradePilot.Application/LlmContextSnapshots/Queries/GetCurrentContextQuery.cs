using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.LlmContextSnapshots.Models;

namespace TradePilot.Application.LlmContextSnapshots.Queries;

public sealed record GetCurrentContextQuery(string Symbol) : Query<LlmContextDto?>;

public sealed class GetCurrentContextQueryHandler : QueryHandler<GetCurrentContextQuery, LlmContextDto?>
{
    private readonly ILlmContextSnapshotRepository _repository;

    public GetCurrentContextQueryHandler(ILlmContextSnapshotRepository repository)
    {
        _repository = repository;
    }

    public override async Task<LlmContextDto?> Handle(
        GetCurrentContextQuery request,
        CancellationToken cancellationToken)
    {
        var snapshot = await _repository.GetLatestAsync(request.Symbol, cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        return new LlmContextDto
        {
            Symbol = snapshot.Symbol,
            MarketSentiment = snapshot.MarketSentiment,
            MacroRegime = snapshot.MacroRegime,
            EventRisk = snapshot.EventRisk,
            Confidence = snapshot.Confidence,
            DerivedRegime = snapshot.DerivedRegime,
            Summary = snapshot.Summary,
            GeneratedAtUtc = snapshot.GeneratedAtUtc,
        };
    }
}
