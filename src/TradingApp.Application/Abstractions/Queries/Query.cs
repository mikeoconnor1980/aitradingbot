using MediatR;

namespace TradingApp.Application.Abstractions.Queries;

public abstract record Query<T> : IRequest<T>;