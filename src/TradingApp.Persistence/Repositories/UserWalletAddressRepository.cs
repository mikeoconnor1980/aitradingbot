using Microsoft.EntityFrameworkCore;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;

namespace TradingApp.Persistence.Repositories;

public sealed class UserWalletAddressRepository : IUserWalletAddressRepository
{
    private readonly TradingAppDbContext _db;

    public UserWalletAddressRepository(TradingAppDbContext db)
    {
        _db = db;
    }

    public async Task<UserWalletAddress?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserWalletAddresses
            .FirstOrDefaultAsync(w => w.UserId == userId && w.IsActive, cancellationToken);
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
