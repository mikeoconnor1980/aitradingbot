using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Backtesting.Models;

namespace TradingApp.Application.Backtesting;

public sealed record GetBacktestResultQuery(Guid Id) : Query<BacktestRunResponse>;

public sealed class GetBacktestResultQueryHandler : QueryHandler<GetBacktestResultQuery, BacktestRunResponse>
{
    private readonly IBacktestRunRepository _repository;

    public GetBacktestResultQueryHandler(IBacktestRunRepository repository)
    {
        _repository = repository;
    }

    public override async Task<BacktestRunResponse> Handle(GetBacktestResultQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException("BacktestRun", request.Id.ToString());
        }

        return BacktestRunResponseMapper.ToResponse(entity);
    }
}