using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, User>> GetByEmailsAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default);
    Task<User?> GetByExternalProviderAsync(string provider, string externalId, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
