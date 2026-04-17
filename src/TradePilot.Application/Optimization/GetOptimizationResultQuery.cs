using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Optimization.Models;

namespace TradePilot.Application.Optimization;

public sealed record GetOptimizationResultQuery(Guid Id) : Query<OptimizationRunResponse>;

public sealed class GetOptimizationResultQueryHandler : QueryHandler<GetOptimizationResultQuery, OptimizationRunResponse>
{
    private readonly IOptimizationRunRepository _repository;

    public GetOptimizationResultQueryHandler(IOptimizationRunRepository repository)
    {
        _repository = repository;
    }

    public override async Task<OptimizationRunResponse> Handle(GetOptimizationResultQuery request, CancellationToken cancellationToken)
    {
        var run = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OptimizationRun", request.Id.ToString());
        var results = await _repository.GetResultsByRunIdAsync(request.Id, cancellationToken);

        return OptimizationRunResponseMapper.ToResponse(run, results);
    }
}