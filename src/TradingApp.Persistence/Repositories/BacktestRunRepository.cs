using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingApp.Application.Abstractions.Models;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Persistence.Repositories;

public sealed class BacktestRunRepository : IBacktestRunRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly TradingAppDbContext _context;

    public BacktestRunRepository(TradingAppDbContext context)
    {
        _context = context;
    }

    public async Task<BacktestRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.BacktestRuns
            .FirstOrDefaultAsync(backtestRun => backtestRun.Id == id, cancellationToken);
    }

    public async Task<PagedResult<BacktestRunSummary>> GetPagedSummariesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await GetPagedSummariesCoreAsync(_context.BacktestRuns, page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<BacktestRunSummary>> GetPagedSummariesByStrategyAsync(
        Guid strategyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.BacktestRuns
            .Where(backtestRun => backtestRun.StrategyId == strategyId);

        return await GetPagedSummariesCoreAsync(query, page, pageSize, cancellationToken);
    }

    private async Task<PagedResult<BacktestRunSummary>> GetPagedSummariesCoreAsync(
        IQueryable<BacktestRun> source,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var totalCount = await source.CountAsync(cancellationToken);

        var projections = await source
            .AsNoTracking()
            .OrderByDescending(backtestRun => backtestRun.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(backtestRun => new
            {
                backtestRun.Id,
                backtestRun.Symbol,
                backtestRun.IntervalsJson,
                backtestRun.StartDateUtc,
                backtestRun.EndDateUtc,
                backtestRun.TotalTrades,
                backtestRun.WinRate,
                backtestRun.TotalPnl,
                backtestRun.MaxDrawdown,
                backtestRun.CreatedAtUtc,
                backtestRun.StrategyId,
                backtestRun.StrategyRevisionId,
            })
            .ToListAsync(cancellationToken);

        var items = projections.Select(backtestRun => new BacktestRunSummary
        {
            Id = backtestRun.Id,
            Symbol = backtestRun.Symbol,
            Intervals = JsonSerializer.Deserialize<string[]>(backtestRun.IntervalsJson, JsonOptions) ?? [],
            StartDate = DateTimeOffset.FromUnixTimeMilliseconds(backtestRun.StartDateUtc).UtcDateTime,
            EndDate = DateTimeOffset.FromUnixTimeMilliseconds(backtestRun.EndDateUtc).UtcDateTime,
            TotalTrades = backtestRun.TotalTrades,
            WinRate = backtestRun.WinRate,
            TotalPnl = backtestRun.TotalPnl,
            MaxDrawdown = backtestRun.MaxDrawdown,
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(backtestRun.CreatedAtUtc).UtcDateTime,
            StrategyId = backtestRun.StrategyId,
            StrategyRevisionId = backtestRun.StrategyRevisionId,
        }).ToList();

        return new PagedResult<BacktestRunSummary>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task AddAsync(BacktestRun backtestRun, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(backtestRun);

        await _context.BacktestRuns.AddAsync(backtestRun, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BacktestRun backtestRun, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(backtestRun);

        _context.BacktestRuns.Update(backtestRun);
        await _context.SaveChangesAsync(cancellationToken);
    }
}