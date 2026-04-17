using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface ILlmContextSnapshotRepository
{
    Task<LlmContextSnapshot?> GetLatestAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LlmContextSnapshot>> GetHistoryAsync(
        string symbol,
        long fromUtc,
        long toUtc,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        LlmContextSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
