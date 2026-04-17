using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;

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
