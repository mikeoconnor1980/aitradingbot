using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;

namespace TradePilot.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly TradePilotDbContext _db;

    public UserRepository(TradePilotDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, User>> GetByEmailsAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emails);

        var normalizedEmails = emails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedEmails.Length == 0)
        {
            return new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
        }

        var users = await _db.Users
            .Where(user => user.IsActive && normalizedEmails.Contains(user.Email))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return users.ToDictionary(user => user.Email, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<User?> GetByExternalProviderAsync(string provider, string externalId, CancellationToken cancellationToken = default)
    {
        return await _db.Users.FirstOrDefaultAsync(
            u => u.AuthProvider == provider && u.ExternalProviderId == externalId && u.IsActive,
            cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _db.Users.AddAsync(user, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}
