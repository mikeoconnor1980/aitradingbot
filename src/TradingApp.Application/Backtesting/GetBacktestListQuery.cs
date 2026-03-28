using TradingApp.Application.Abstractions.Models;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Backtesting.Models;

namespace TradingApp.Application.Backtesting;

public sealed record GetBacktestListQuery(int Page, int PageSize) : Query<PagedResult<BacktestRunSummary>>;

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

        return await _repository.GetPagedSummariesAsync(request.Page, request.PageSize, cancellationToken);
    }
}