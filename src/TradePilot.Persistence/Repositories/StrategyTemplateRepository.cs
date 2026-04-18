using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;

namespace TradePilot.Persistence.Repositories;

public sealed class StrategyTemplateRepository : IStrategyTemplateRepository
{
    private readonly TradePilotDbContext _context;

    public StrategyTemplateRepository(TradePilotDbContext context)
    {
        _context = context;
    }

    public async Task<StrategyTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.StrategyTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<StrategyTemplate?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.StrategyTemplates
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<List<StrategyTemplate>> GetActiveOrderedAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StrategyTemplates
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsWithNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim().ToUpperInvariant();

        return await _context.StrategyTemplates
            .Where(t => t.IsActive)
            .AnyAsync(t => t.Name.ToUpper() == normalizedName, cancellationToken);
    }

    public async Task<bool> ExistsWithSlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.StrategyTemplates
            .AnyAsync(t => t.Slug == slug, cancellationToken);
    }

    public async Task<int> GetNextSortOrderAsync(CancellationToken cancellationToken = default)
    {
        var maxSortOrder = await _context.StrategyTemplates
            .Select(t => (int?)t.SortOrder)
            .MaxAsync(cancellationToken);

        return (maxSortOrder ?? 0) + 1;
    }

    public async Task AddAsync(StrategyTemplate template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        await _context.StrategyTemplates.AddAsync(template, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
