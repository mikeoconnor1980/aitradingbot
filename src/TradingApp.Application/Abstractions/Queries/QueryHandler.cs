using MediatR;

namespace TradingApp.Application.Abstractions.Queries;

public abstract class QueryHandler<TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : Query<TResult>
{
    public abstract Task<TResult> Handle(TQuery request, CancellationToken cancellationToken);
}