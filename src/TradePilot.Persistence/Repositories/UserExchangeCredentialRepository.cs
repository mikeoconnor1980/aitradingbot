using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Persistence.Repositories;

public sealed class UserExchangeCredentialRepository : IUserExchangeCredentialRepository
{
    private readonly TradePilotDbContext _db;

    public UserExchangeCredentialRepository(TradePilotDbContext db)
    {
        _db = db;
    }

    public async Task<UserExchangeCredential?> GetActiveByUserIdAndExchangeAsync(Guid userId, Exchange exchange, CancellationToken cancellationToken = default)
    {
        return await _db.UserExchangeCredentials
            .FirstOrDefaultAsync(
                credential => credential.UserId == userId && credential.Exchange == exchange && credential.IsActive,
                cancellationToken);
    }

    public async Task<IReadOnlyList<UserExchangeCredential>> GetAllActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserExchangeCredentials
            .Where(credential => credential.UserId == userId && credential.IsActive)
            .OrderBy(credential => credential.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserExchangeCredential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.UserExchangeCredentials.FirstOrDefaultAsync(credential => credential.Id == id, cancellationToken);
    }

    public async Task AddAsync(UserExchangeCredential credential, CancellationToken cancellationToken = default)
    {
        await _db.UserExchangeCredentials.AddAsync(credential, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}