using Microsoft.EntityFrameworkCore;
using TradingApp.Application.Abstractions.Models;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Optimization.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Persistence.Repositories;

public sealed class OptimizationRunRepository : IOptimizationRunRepository
{
    private readonly TradingAppDbContext _context;

    public OptimizationRunRepository(TradingAppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OptimizationRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        await _context.OptimizationRuns.AddAsync(run, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OptimizationRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        _context.OptimizationRuns.Update(run);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<OptimizationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.OptimizationRuns.FirstOrDefaultAsync(run => run.Id == id, cancellationToken);
    }

    public async Task<PagedResult<OptimizationRunSummary>> GetPagedListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var totalCount = await _context.OptimizationRuns.CountAsync(cancellationToken);

        var runs = await _context.OptimizationRuns
            .AsNoTracking()
            .OrderByDescending(run => run.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(run => new
            {
                run.Id,
                run.Symbol,
                run.Status,
                run.TotalCombinations,
                run.CompletedCount,
                run.QualifiedCount,
                run.ElapsedMs,
                run.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var runIds = runs.Select(run => run.Id).ToArray();
        var topResults = runIds.Length == 0
            ? []
            : await _context.OptimizationResults
                .AsNoTracking()
                .Where(result => runIds.Contains(result.OptimizationRunId) && result.Rank == 1)
                .Select(result => new
                {
                    result.OptimizationRunId,
                    result.FitnessScore,
                    result.TotalPnl,
                    result.WinRate,
                    result.SignalDescription,
                })
                .ToListAsync(cancellationToken);

        var topResultsByRunId = topResults.ToDictionary(result => result.OptimizationRunId);

        var items = runs.Select(run =>
        {
            topResultsByRunId.TryGetValue(run.Id, out var topResult);

            return new OptimizationRunSummary
            {
                Id = run.Id,
                Symbol = run.Symbol,
                Status = run.Status.ToString(),
                TotalCombinations = run.TotalCombinations,
                CompletedCount = run.CompletedCount,
                QualifiedCount = run.QualifiedCount,
                ElapsedMs = run.ElapsedMs,
                CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(run.CreatedAtUtc).UtcDateTime,
                TopFitnessScore = topResult?.FitnessScore,
                TopTotalPnl = topResult?.TotalPnl,
                TopWinRate = topResult?.WinRate,
                TopSignalDescription = topResult?.SignalDescription,
            };
        }).ToList();

        return new PagedResult<OptimizationRunSummary>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task AddResultsAsync(IReadOnlyList<OptimizationResult> results, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count == 0)
        {
            return;
        }

        await _context.OptimizationResults.AddRangeAsync(results, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OptimizationResult>> GetResultsByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return await _context.OptimizationResults
            .AsNoTracking()
            .Where(result => result.OptimizationRunId == runId)
            .OrderBy(result => result.Rank)
            .ToListAsync(cancellationToken);
    }
}