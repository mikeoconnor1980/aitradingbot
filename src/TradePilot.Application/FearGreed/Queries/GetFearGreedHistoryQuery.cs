using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.FearGreed.Models;

namespace TradePilot.Application.FearGreed.Queries;

public sealed record GetFearGreedHistoryQuery(long? FromTimestamp, long? ToTimestamp) : Query<IReadOnlyList<FearGreedReadingDto>>;

public sealed class GetFearGreedHistoryQueryHandler
    : QueryHandler<GetFearGreedHistoryQuery, IReadOnlyList<FearGreedReadingDto>>
{
    private readonly IFearGreedReadingRepository _repository;

    public GetFearGreedHistoryQueryHandler(IFearGreedReadingRepository repository)
    {
        _repository = repository;
    }

    public override async Task<IReadOnlyList<FearGreedReadingDto>> Handle(
        GetFearGreedHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromTimestamp ?? 0;
        var to = request.ToTimestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var readings = await _repository.GetRangeAsync(from, to, cancellationToken);

        return readings
            .Select(r => new FearGreedReadingDto(r.Value, r.Classification, r.Timestamp))
            .ToList();
    }
}
