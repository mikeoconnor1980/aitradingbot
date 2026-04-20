using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Persistence.Repositories;

public sealed class UserWalletAddressRepository : IUserWalletAddressRepository
{
    private readonly TradePilotDbContext _db;

    public UserWalletAddressRepository(TradePilotDbContext db)
    {
        _db = db;
    }

    public async Task<UserWalletAddress?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserWalletAddresses
            .FirstOrDefaultAsync(w => w.UserId == userId && w.IsActive, cancellationToken);
    }

    public async Task<UserWalletAddress?> GetActiveByUserIdAndExchangeAsync(Guid userId, Exchange exchange, CancellationToken cancellationToken = default)
    {
        return await _db.UserWalletAddresses
            .FirstOrDefaultAsync(
                wallet => wallet.UserId == userId && wallet.IsActive && wallet.Exchange == exchange.ToString(),
                cancellationToken);
    }

    public async Task<UserWalletAddress?> GetActiveByWalletAddressAsync(string walletAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(walletAddress);

        return await _db.UserWalletAddresses
            .FirstOrDefaultAsync(
                wallet => wallet.IsActive && wallet.WalletAddress == walletAddress,
                cancellationToken);
    }

    public async Task<IReadOnlyList<UserWalletAddress>> GetAllActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserWalletAddresses
            .Where(wallet => wallet.UserId == userId && wallet.IsActive)
            .OrderBy(wallet => wallet.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(UserWalletAddress walletAddress, CancellationToken cancellationToken = default)
    {
        await _db.UserWalletAddresses.AddAsync(walletAddress, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}
