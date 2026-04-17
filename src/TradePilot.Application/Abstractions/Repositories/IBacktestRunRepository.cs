using TradePilot.Domain.Entities;
using TradePilot.Application.Abstractions.Models;
using TradePilot.Application.Backtesting.Models;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IBacktestRunRepository
{
    Task<BacktestRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<BacktestRunSummary>> GetPagedSummariesByStrategyAsync(
        Guid strategyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<PagedResult<BacktestRunSummary>> GetPagedSummariesAsync(
        int page,
        int pageSize,
        string? symbol = null,
        IReadOnlyList<Guid>? strategyIds = null,
        CancellationToken cancellationToken = default);
    Task<BacktestRun?> GetLatestCompletedByStrategyRevisionAsync(
        Guid strategyId,
        int revisionNumber,
        CancellationToken cancellationToken = default);
    Task AddAsync(BacktestRun backtestRun, CancellationToken cancellationToken = default);
    Task UpdateAsync(BacktestRun backtestRun, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}