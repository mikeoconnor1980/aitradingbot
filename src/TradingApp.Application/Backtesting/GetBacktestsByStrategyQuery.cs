using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Identity;
using TradingApp.Application.Abstractions.Models;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Backtesting;

public sealed record GetBacktestsByStrategyQuery(
    Guid StrategyId,
    int Page,
    int PageSize,
    AppIdentity Identity) : Query<PagedResult<BacktestRunSummary>>;

public sealed class GetBacktestsByStrategyQueryHandler
    : QueryHandler<GetBacktestsByStrategyQuery, PagedResult<BacktestRunSummary>>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly IBacktestRunRepository _backtestRunRepository;

    public GetBacktestsByStrategyQueryHandler(
        IStrategyRepository strategyRepository,
        IBacktestRunRepository backtestRunRepository)
    {
        _strategyRepository = strategyRepository;
        _backtestRunRepository = backtestRunRepository;
    }

    public override async Task<PagedResult<BacktestRunSummary>> Handle(
        GetBacktestsByStrategyQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Identity);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.PageSize, 100);

        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Strategy), request.StrategyId);

        if (strategy.UserId != request.Identity.UserId || !strategy.IsActive)
        {
            throw new NotFoundException(nameof(Strategy), request.StrategyId);
        }

        return await _backtestRunRepository.GetPagedSummariesByStrategyAsync(
            request.StrategyId,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}