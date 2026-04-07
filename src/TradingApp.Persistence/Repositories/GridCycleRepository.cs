using Microsoft.EntityFrameworkCore;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;

namespace TradingApp.Persistence.Repositories;

public sealed class GridCycleRepository : IGridCycleRepository
{
    private readonly TradingAppDbContext _context;

    public GridCycleRepository(TradingAppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(GridCycle cycle, CancellationToken cancellationToken = default)
    {
        _context.GridCycles.Add(cycle);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(GridCycle cycle, CancellationToken cancellationToken = default)
    {
        _context.GridCycles.Update(cycle);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<GridCycle?> GetByGridCycleIdAsync(string gridCycleId, CancellationToken cancellationToken = default)
    {
        return await _context.GridCycles
            .FirstOrDefaultAsync(c => c.GridCycleId == gridCycleId, cancellationToken);
    }

    public async Task<GridCycle?> GetActiveForStrategyAsync(
        string strategyName, string symbol, CancellationToken cancellationToken = default)
    {
        return await _context.GridCycles
            .Where(c => c.StrategyName == strategyName
                && c.Symbol == symbol
                && c.Lifecycle != "Closed"
                && c.Lifecycle != "Inactive")
            .OrderByDescending(c => c.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GridCycle>> GetBySymbolAsync(
        string symbol, string? lifecycle = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        var query = _context.GridCycles.Where(c => c.Symbol == symbol);

        if (lifecycle is not null)
        {
            query = query.Where(c => c.Lifecycle == lifecycle);
        }

        return await query
            .OrderByDescending(c => c.StartedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
