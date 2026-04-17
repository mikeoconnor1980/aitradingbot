using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;

namespace TradePilot.Persistence.Repositories;

public sealed class LlmContextSnapshotRepository : ILlmContextSnapshotRepository
{
    private readonly TradePilotDbContext _context;

    public LlmContextSnapshotRepository(TradePilotDbContext context)
    {
        _context = context;
    }

    public async Task<LlmContextSnapshot?> GetLatestAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        return await _context.LlmContextSnapshots
            .Where(s => s.Symbol == symbol)
            .OrderByDescending(s => s.GeneratedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LlmContextSnapshot>> GetHistoryAsync(
        string symbol,
        long fromUtc,
        long toUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        return await _context.LlmContextSnapshots
            .Where(s => s.Symbol == symbol
                && s.GeneratedAtUtc >= fromUtc
                && s.GeneratedAtUtc <= toUtc)
            .OrderBy(s => s.GeneratedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(
        LlmContextSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _context.LlmContextSnapshots.Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
