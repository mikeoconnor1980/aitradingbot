using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.FearGreed.Models;

namespace TradePilot.Application.FearGreed.Queries;

public sealed record GetFearGreedStatusQuery : Query<FearGreedStatusDto>;

public sealed class GetFearGreedStatusQueryHandler
    : QueryHandler<GetFearGreedStatusQuery, FearGreedStatusDto>
{
    private readonly IFearGreedReadingRepository _repository;

    public GetFearGreedStatusQueryHandler(IFearGreedReadingRepository repository)
    {
        _repository = repository;
    }

    public override async Task<FearGreedStatusDto> Handle(
        GetFearGreedStatusQuery request,
        CancellationToken cancellationToken)
    {
        var latest = await _repository.GetLatestAsync(cancellationToken);
        var earliest = await _repository.GetEarliestAsync(cancellationToken);
        var count = await _repository.GetCountAsync(cancellationToken);

        return new FearGreedStatusDto(
            LatestValue: latest?.Value,
            LatestClassification: latest?.Classification,
            LatestTimestamp: latest is not null
                ? DateTimeOffset.FromUnixTimeSeconds(latest.Timestamp)
                : null,
            TotalReadings: count,
            EarliestTimestamp: earliest is not null
                ? DateTimeOffset.FromUnixTimeSeconds(earliest.Timestamp)
                : null);
    }
}
