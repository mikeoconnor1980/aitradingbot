using MediatR;

namespace TradingApp.Application.Abstractions.Commands;

public abstract record Command : IRequest<Unit>;

public abstract record Command<T> : IRequest<T>;

public abstract record CreateCommand : IRequest<Guid>;