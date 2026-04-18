using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IAdminUserGrantRepository
{
    Task<IReadOnlyList<AdminUserGrant>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AdminUserGrant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdminUserGrant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task AddAsync(AdminUserGrant grant, CancellationToken cancellationToken = default);
    Task RemoveAsync(AdminUserGrant grant, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}