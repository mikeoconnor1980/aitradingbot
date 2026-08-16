using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.TradeJournal.Models;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Persistence.Repositories;

/// <summary>EF Core persistence and database-side aggregation for logical live trades.</summary>
public sealed class TradeJournalRepository : ITradeJournalRepository
{
    private readonly TradePilotDbContext _context;
    private readonly ILogger<TradeJournalRepository> _logger;

    public TradeJournalRepository(
        TradePilotDbContext context,
        ILogger<TradeJournalRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<TradeJournalRecord?> GetOpenAsync(
        string userId,
        string symbol,
        Guid? strategyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var query = _context.TradeJournalRecords.Where(trade =>
            trade.UserId == userId
            && trade.Symbol == normalizedSymbol
            && trade.Status != TradeLifecycleStatus.Closed);
        if (strategyId.HasValue)
        {
            query = query.Where(trade => trade.StrategyId == strategyId.Value);
        }
        return query
            .OrderByDescending(trade => trade.EntryTimeUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(TradeJournalRecord trade, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trade);
        await _context.TradeJournalRecords.AddAsync(trade, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TradeJournalRecord trade, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trade);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<TradeJournalRecord?> GetByIdAsync(
        Guid tradeId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _context.TradeJournalRecords
            .AsNoTracking()
            .Include(trade => trade.EntryStrategyEvaluation)
                .ThenInclude(evaluation => evaluation!.Rules)
            .Include(trade => trade.ExitStrategyEvaluation)
                .ThenInclude(evaluation => evaluation!.Rules)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                trade => trade.Id == tradeId && trade.UserId == userId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<TradeJournalRecord>> GetAsync(
        TradeJournalFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var stopwatch = Stopwatch.StartNew();
        var result = await ApplyFilter(_context.TradeJournalRecords.AsNoTracking(), filter)
            .OrderByDescending(trade => trade.ExitTimeUtc ?? trade.EntryTimeUtc)
            .ThenByDescending(trade => trade.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
        _logger.LogInformation(
            "Trade journal query returned {Count} rows in {DurationMs}ms",
            result.Count,
            stopwatch.ElapsedMilliseconds);
        return result;
    }

    public async Task<TradeAnalytics> GetAnalyticsAsync(
        TradeJournalFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var stopwatch = Stopwatch.StartNew();
        var analytics = await CalculateAnalyticsAsync(
            ApplyFilter(_context.TradeJournalRecords.AsNoTracking(), filter)
                .Where(trade => trade.Status == TradeLifecycleStatus.Closed),
            cancellationToken);
        _logger.LogInformation(
            "Trade analytics query aggregated {Count} completed trades in {DurationMs}ms",
            analytics.TradeCount,
            stopwatch.ElapsedMilliseconds);
        return analytics;
    }

    public async Task<StrategyTradeAnalytics> GetStrategyAnalyticsAsync(
        TradeJournalFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var baseQuery = ApplyFilter(_context.TradeJournalRecords.AsNoTracking(), filter)
            .Where(trade => trade.Status == TradeLifecycleStatus.Closed);
        var versions = await baseQuery
            .Select(trade => trade.StrategyVersion)
            .Distinct()
            .OrderBy(version => version)
            .ToListAsync(cancellationToken);
        var regimes = await baseQuery
            .Select(trade => trade.EntryMarketRegime)
            .Distinct()
            .OrderBy(regime => regime)
            .ToListAsync(cancellationToken);

        var byVersion = new List<TradeAnalyticsGroup>();
        foreach (var version in versions)
        {
            var groupQuery = version.HasValue
                ? baseQuery.Where(trade => trade.StrategyVersion == version.Value)
                : baseQuery.Where(trade => trade.StrategyVersion == null);
            byVersion.Add(new TradeAnalyticsGroup(
                version?.ToString() ?? "Unavailable",
                await CalculateAnalyticsAsync(groupQuery, cancellationToken)));
        }

        var byRegime = new List<TradeAnalyticsGroup>();
        foreach (var regime in regimes)
        {
            var groupQuery = regime is not null
                ? baseQuery.Where(trade => trade.EntryMarketRegime == regime)
                : baseQuery.Where(trade => trade.EntryMarketRegime == null);
            byRegime.Add(new TradeAnalyticsGroup(
                regime ?? "Unavailable",
                await CalculateAnalyticsAsync(groupQuery, cancellationToken)));
        }

        return new StrategyTradeAnalytics(byVersion, byRegime);
    }

    private static IQueryable<TradeJournalRecord> ApplyFilter(
        IQueryable<TradeJournalRecord> query,
        TradeJournalFilter filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filter.UserId);
        query = query.Where(trade => trade.UserId == filter.UserId);

        if (filter.ClosedOnly)
        {
            query = query.Where(trade => trade.Status == TradeLifecycleStatus.Closed);
        }

        if (filter.StrategyId.HasValue)
        {
            query = query.Where(trade => trade.StrategyId == filter.StrategyId.Value);
        }

        if (filter.StrategyVersion.HasValue)
        {
            query = query.Where(trade => trade.StrategyVersion == filter.StrategyVersion.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Symbol))
        {
            var symbol = filter.Symbol.Trim().ToUpperInvariant();
            query = query.Where(trade => trade.Symbol == symbol);
        }

        if (filter.Side.HasValue)
        {
            query = query.Where(trade => trade.Side == filter.Side.Value);
        }

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(trade => trade.ExitTimeUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(trade => trade.ExitTimeUtc <= filter.ToUtc.Value);
        }

        query = filter.Outcome switch
        {
            TradeOutcome.Winner => query.Where(trade => trade.NetPnl > 0m),
            TradeOutcome.Loser => query.Where(trade => trade.NetPnl < 0m),
            TradeOutcome.Breakeven => query.Where(trade => trade.NetPnl == 0m),
            _ => query,
        };

        return query;
    }

    private static async Task<TradeAnalytics> CalculateAnalyticsAsync(
        IQueryable<TradeJournalRecord> query,
        CancellationToken cancellationToken)
    {
        var tradeCount = await query.CountAsync(cancellationToken);
        if (tradeCount == 0)
        {
            return new TradeAnalytics(
                0, 0, 0, 0, 0m, 0m, 0m, null, true, 0m, null, null, 0m,
                null, true, null, null, null, null, null, null, null);
        }

        var winningTrades = await query.CountAsync(trade => trade.NetPnl > 0m, cancellationToken);
        var losingTrades = await query.CountAsync(trade => trade.NetPnl < 0m, cancellationToken);
        var breakevenTrades = tradeCount - winningTrades - losingTrades;
        var grossPnl = await query.SumAsync(trade => trade.GrossPnl, cancellationToken);
        var netPnl = await query.SumAsync(trade => trade.NetPnl, cancellationToken);
        var fees = await query.SumAsync(trade => trade.Fees, cancellationToken);
        var missingFunding = await query.CountAsync(trade => trade.Funding == null, cancellationToken);
        var funding = missingFunding == 0
            ? await query.SumAsync(trade => trade.Funding!.Value, cancellationToken)
            : (decimal?)null;
        var averageWin = winningTrades == 0
            ? null
            : await query.Where(trade => trade.NetPnl > 0m).AverageAsync(trade => (decimal?)trade.NetPnl, cancellationToken);
        var averageLoss = losingTrades == 0
            ? null
            : await query.Where(trade => trade.NetPnl < 0m).AverageAsync(trade => (decimal?)trade.NetPnl, cancellationToken);
        var grossProfit = await query.Where(trade => trade.NetPnl > 0m).SumAsync(trade => (decimal?)trade.NetPnl, cancellationToken) ?? 0m;
        var absoluteGrossLoss = Math.Abs(
            await query.Where(trade => trade.NetPnl < 0m).SumAsync(trade => (decimal?)trade.NetPnl, cancellationToken) ?? 0m);
        var averageDurationMs = await query.AverageAsync(trade => (double?)trade.DurationMilliseconds, cancellationToken);
        var averageMfeAmount = await query.AverageAsync(trade => (decimal?)trade.MfeAmount, cancellationToken);
        var averageMfePercent = await query.AverageAsync(trade => (decimal?)trade.MfePercent, cancellationToken);
        var averageMaeAmount = await query.AverageAsync(trade => (decimal?)trade.MaeAmount, cancellationToken);
        var averageMaePercent = await query.AverageAsync(trade => (decimal?)trade.MaePercent, cancellationToken);
        var best = await query
            .OrderByDescending(trade => trade.NetPnl)
            .ThenByDescending(trade => trade.ExitTimeUtc)
            .Select(trade => new TradeExtremum(trade.Id, trade.Symbol, trade.NetPnl, trade.ExitTimeUtc!.Value))
            .FirstAsync(cancellationToken);
        var worst = await query
            .OrderBy(trade => trade.NetPnl)
            .ThenByDescending(trade => trade.ExitTimeUtc)
            .Select(trade => new TradeExtremum(trade.Id, trade.Symbol, trade.NetPnl, trade.ExitTimeUtc!.Value))
            .FirstAsync(cancellationToken);

        return new TradeAnalytics(
            tradeCount,
            winningTrades,
            losingTrades,
            breakevenTrades,
            grossPnl,
            netPnl,
            fees,
            funding,
            missingFunding == 0,
            decimal.Round((decimal)winningTrades / tradeCount * 100m, 4),
            averageWin,
            averageLoss,
            netPnl / tradeCount,
            absoluteGrossLoss == 0m ? null : grossProfit / absoluteGrossLoss,
            absoluteGrossLoss == 0m,
            averageDurationMs.HasValue ? TimeSpan.FromMilliseconds(averageDurationMs.Value) : null,
            averageMfeAmount,
            averageMfePercent,
            averageMaeAmount,
            averageMaePercent,
            best,
            worst);
    }
}
