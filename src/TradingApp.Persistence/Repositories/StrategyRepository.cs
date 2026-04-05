using Microsoft.EntityFrameworkCore;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;

namespace TradingApp.Persistence.Repositories;

public sealed class StrategyRepository : IStrategyRepository
{
    private readonly TradingAppDbContext _context;

    public StrategyRepository(TradingAppDbContext context)
    {
        _context = context;
    }

    public async Task<Strategy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Strategies
            .FirstOrDefaultAsync(strategy => strategy.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Strategy>> GetByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.Strategies
            .AsNoTracking()
            .Where(strategy => ids.Contains(strategy.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Strategy>> GetActiveByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Strategies
            .AsNoTracking()
            .Where(strategy => strategy.UserId == userId && strategy.IsActive)
            .OrderByDescending(strategy => strategy.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsWithNameAsync(
        string userId,
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Strategies
            .Where(strategy => strategy.UserId == userId && strategy.Name == name && strategy.IsActive);

        if (excludeId.HasValue)
        {
            query = query.Where(strategy => strategy.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> SearchIdsByNameAsync(
        string nameContains,
        CancellationToken cancellationToken = default)
    {
        return await _context.Strategies
            .AsNoTracking()
            .Where(strategy => EF.Functions.Like(strategy.Name, $"%{nameContains}%"))
            .Select(strategy => strategy.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Strategy strategy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        await _context.Strategies.AddAsync(strategy, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Strategy strategy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        _context.Strategies.Update(strategy);
        await _context.SaveChangesAsync(cancellationToken);
    }
}