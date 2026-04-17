using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;

namespace TradePilot.Persistence.Repositories;

public sealed class FearGreedReadingRepository : IFearGreedReadingRepository
{
    private readonly TradePilotDbContext _context;

    public FearGreedReadingRepository(TradePilotDbContext context)
    {
        _context = context;
    }

    public async Task<FearGreedReading?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FearGreedReadings
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FearGreedReading>> GetRangeAsync(
        long fromTimestamp,
        long toTimestamp,
        CancellationToken cancellationToken = default)
    {
        return await _context.FearGreedReadings
            .Where(r => r.Timestamp >= fromTimestamp && r.Timestamp <= toTimestamp)
            .OrderBy(r => r.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FearGreedReadings.CountAsync(cancellationToken);
    }

    public async Task<FearGreedReading?> GetEarliestAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FearGreedReadings
            .OrderBy(r => r.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task BulkUpsertAsync(
        IReadOnlyList<FearGreedReading> readings,
        CancellationToken cancellationToken = default)
    {
        if (readings.Count == 0)
        {
            return;
        }

        var timestamps = readings.Select(r => r.Timestamp).ToHashSet();

        var existing = await _context.FearGreedReadings
            .Where(r => timestamps.Contains(r.Timestamp))
            .Select(r => r.Timestamp)
            .ToHashSetAsync(cancellationToken);

        var newReadings = readings.Where(r => !existing.Contains(r.Timestamp)).ToList();

        if (newReadings.Count > 0)
        {
            _context.FearGreedReadings.AddRange(newReadings);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
