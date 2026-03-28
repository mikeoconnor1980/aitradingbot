using TradingApp.Domain.Entities;
using TradingApp.Application.Abstractions.Models;
using TradingApp.Application.Backtesting.Models;

namespace TradingApp.Application.Abstractions.Repositories;

public interface IBacktestRunRepository
{
    Task<BacktestRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<BacktestRunSummary>> GetPagedSummariesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task AddAsync(BacktestRun backtestRun, CancellationToken cancellationToken = default);
}