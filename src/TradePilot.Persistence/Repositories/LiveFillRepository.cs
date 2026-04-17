using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;

namespace TradePilot.Persistence.Repositories;

public sealed class LiveFillRepository : ILiveFillRepository
{
    private readonly TradePilotDbContext _context;

    public LiveFillRepository(TradePilotDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(LiveFill fill, CancellationToken cancellationToken = default)
    {
        _context.LiveFills.Add(fill);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LiveFill>> GetBySymbolAsync(
        string symbol, DateTime? since = null, int limit = 100, CancellationToken cancellationToken = default)
    {
        var query = _context.LiveFills.Where(f => f.Symbol == symbol);

        if (since.HasValue)
        {
            query = query.Where(f => f.FilledAtUtc >= since.Value);
        }

        return await query
            .OrderByDescending(f => f.FilledAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
