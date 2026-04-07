using Microsoft.EntityFrameworkCore;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;

namespace TradingApp.Persistence.Repositories;

public sealed class LiveOrderRepository : ILiveOrderRepository
{
    private readonly TradingAppDbContext _context;

    public LiveOrderRepository(TradingAppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(LiveOrder order, CancellationToken cancellationToken = default)
    {
        _context.LiveOrders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(LiveOrder order, CancellationToken cancellationToken = default)
    {
        _context.LiveOrders.Update(order);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<LiveOrder?> GetByOrderIdAsync(string orderId, CancellationToken cancellationToken = default)
    {
        return await _context.LiveOrders
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
    }

    public async Task<IReadOnlyList<LiveOrder>> GetByGridCycleIdAsync(string gridCycleId, CancellationToken cancellationToken = default)
    {
        return await _context.LiveOrders
            .Where(o => o.GridCycleId == gridCycleId)
            .OrderBy(o => o.Level)
            .ToListAsync(cancellationToken);
    }
}
