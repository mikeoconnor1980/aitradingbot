using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Models;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;

namespace TradePilot.Persistence.Repositories;

public sealed class StrategyRevisionRepository : IStrategyRevisionRepository
{
    private readonly TradePilotDbContext _context;

    public StrategyRevisionRepository(TradePilotDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(StrategyRevision revision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revision);

        await _context.StrategyRevisions.AddAsync(revision, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<StrategyRevision?> GetByStrategyAndRevisionAsync(
        Guid strategyId,
        int revisionNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.StrategyRevisions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                revision => revision.StrategyId == strategyId && revision.RevisionNumber == revisionNumber,
                cancellationToken);
    }

    public async Task<PagedResult<StrategyRevision>> GetPagedByStrategyIdAsync(
        Guid strategyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var query = _context.StrategyRevisions
            .AsNoTracking()
            .Where(revision => revision.StrategyId == strategyId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(revision => revision.RevisionNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StrategyRevision>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<int> GetLatestRevisionNumberAsync(
        Guid strategyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.StrategyRevisions
            .Where(revision => revision.StrategyId == strategyId)
            .MaxAsync(revision => (int?)revision.RevisionNumber, cancellationToken) ?? 0;
    }
}