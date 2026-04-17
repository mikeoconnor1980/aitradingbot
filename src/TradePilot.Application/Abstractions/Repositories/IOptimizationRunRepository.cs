using TradePilot.Application.Abstractions.Models;
using TradePilot.Application.Optimization.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IOptimizationRunRepository
{
    Task AddAsync(OptimizationRun run, CancellationToken cancellationToken = default);
    Task UpdateAsync(OptimizationRun run, CancellationToken cancellationToken = default);
    Task<OptimizationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<OptimizationRunSummary>> GetPagedListAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task AddResultsAsync(IReadOnlyList<OptimizationResult> results, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OptimizationResult>> GetResultsByRunIdAsync(Guid runId, CancellationToken cancellationToken = default);
}