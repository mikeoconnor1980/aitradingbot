using TradePilot.Application.Abstractions.Models;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Optimization.Models;

namespace TradePilot.Application.Optimization;

public sealed record GetOptimizationListQuery(int Page, int PageSize) : Query<PagedResult<OptimizationRunSummary>>;

public sealed class GetOptimizationListQueryHandler : QueryHandler<GetOptimizationListQuery, PagedResult<OptimizationRunSummary>>
{
    private readonly IOptimizationRunRepository _repository;

    public GetOptimizationListQueryHandler(IOptimizationRunRepository repository)
    {
        _repository = repository;
    }

    public override async Task<PagedResult<OptimizationRunSummary>> Handle(
        GetOptimizationListQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.PageSize, 100);

        return await _repository.GetPagedListAsync(request.Page, request.PageSize, cancellationToken);
    }
}