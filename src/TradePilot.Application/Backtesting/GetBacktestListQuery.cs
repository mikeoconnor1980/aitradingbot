using TradePilot.Application.Abstractions.Models;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Backtesting.Models;

namespace TradePilot.Application.Backtesting;

public sealed record GetBacktestListQuery(
    int Page,
    int PageSize,
    string? Symbol = null,
    IReadOnlyList<Guid>? StrategyIds = null) : Query<PagedResult<BacktestRunSummary>>;

public sealed class GetBacktestListQueryHandler : QueryHandler<GetBacktestListQuery, PagedResult<BacktestRunSummary>>
{
    private readonly IBacktestRunRepository _repository;

    public GetBacktestListQueryHandler(IBacktestRunRepository repository)
    {
        _repository = repository;
    }

    public override async Task<PagedResult<BacktestRunSummary>> Handle(
        GetBacktestListQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.PageSize, 100);

        return await _repository.GetPagedSummariesAsync(
            request.Page,
            request.PageSize,
            request.Symbol,
            request.StrategyIds,
            cancellationToken);
    }
}