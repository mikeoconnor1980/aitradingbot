using Microsoft.EntityFrameworkCore;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;

namespace TradingApp.Persistence.Repositories;

public sealed class StrategyReviewRepository : IStrategyReviewRepository
{
    private readonly TradingAppDbContext _context;

    public StrategyReviewRepository(TradingAppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(StrategyReview review, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);

        await _context.StrategyReviews.AddAsync(review, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<StrategyReview?> GetByStrategyAndRevisionAsync(
        Guid strategyId,
        int revisionNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.StrategyReviews
            .AsNoTracking()
            .FirstOrDefaultAsync(
                review => review.StrategyId == strategyId && review.RevisionNumber == revisionNumber,
                cancellationToken);
    }

    public async Task DeleteByStrategyAndRevisionAsync(
        Guid strategyId,
        int revisionNumber,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.StrategyReviews
            .FirstOrDefaultAsync(
                review => review.StrategyId == strategyId && review.RevisionNumber == revisionNumber,
                cancellationToken);

        if (existing is null)
        {
            return;
        }

        _context.StrategyReviews.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
    }
}