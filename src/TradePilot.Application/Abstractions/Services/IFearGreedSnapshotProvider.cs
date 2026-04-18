using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.Abstractions.Services;

public interface IFearGreedSnapshotProvider
{
    Task<FearGreedSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default);
}