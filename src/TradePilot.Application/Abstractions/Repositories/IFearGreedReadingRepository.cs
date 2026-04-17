using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IFearGreedReadingRepository
{
    Task<FearGreedReading?> GetLatestAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FearGreedReading>> GetRangeAsync(
        long fromTimestamp,
        long toTimestamp,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    Task<FearGreedReading?> GetEarliestAsync(CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IReadOnlyList<FearGreedReading> readings,
        CancellationToken cancellationToken = default);
}
