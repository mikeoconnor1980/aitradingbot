using TradingApp.Domain.Entities;

namespace TradingApp.Application.Abstractions.Repositories;

public interface IUserWalletAddressRepository
{
    Task<UserWalletAddress?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserWalletAddress walletAddress, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
