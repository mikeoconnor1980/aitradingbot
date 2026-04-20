using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IUserExchangeCredentialRepository
{
    Task<UserExchangeCredential?> GetActiveByUserIdAndExchangeAsync(Guid userId, Exchange exchange, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserExchangeCredential>> GetAllActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserExchangeCredential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(UserExchangeCredential credential, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}