using MediatR;

namespace TradePilot.Application.Abstractions.Queries;

public abstract record Query<T> : IRequest<T>;