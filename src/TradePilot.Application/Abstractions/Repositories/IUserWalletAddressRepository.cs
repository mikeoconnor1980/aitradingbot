using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IUserWalletAddressRepository
{
    Task<UserWalletAddress?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserWalletAddress?> GetActiveByUserIdAndExchangeAsync(Guid userId, Exchange exchange, CancellationToken cancellationToken = default);
    Task<UserWalletAddress?> GetActiveByWalletAddressAsync(string walletAddress, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserWalletAddress>> GetAllActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserWalletAddress walletAddress, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
