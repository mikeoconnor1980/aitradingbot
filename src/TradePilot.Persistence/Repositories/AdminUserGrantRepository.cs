using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;

namespace TradePilot.Persistence.Repositories;

public sealed class AdminUserGrantRepository : IAdminUserGrantRepository
{
    private readonly TradePilotDbContext _context;

    public AdminUserGrantRepository(TradePilotDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AdminUserGrant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AdminUserGrants
            .OrderBy(grant => grant.Email)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminUserGrant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AdminUserGrants
            .FirstOrDefaultAsync(grant => grant.Id == id, cancellationToken);
    }

    public async Task<AdminUserGrant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = AdminUserGrant.NormalizeEmail(email);

        return await _context.AdminUserGrants
            .FirstOrDefaultAsync(grant => grant.Email == normalizedEmail, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = AdminUserGrant.NormalizeEmail(email);

        return await _context.AdminUserGrants
            .AnyAsync(grant => grant.Email == normalizedEmail, cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AdminUserGrants.CountAsync(cancellationToken);
    }

    public async Task AddAsync(AdminUserGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);

        await _context.AdminUserGrants.AddAsync(grant, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(AdminUserGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);

        _context.AdminUserGrants.Remove(grant);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}