using MediatR;
using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Identity;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.StrategyAuthoring.Commands;

public sealed record DeleteStrategyCommand(Guid Id, AppIdentity Identity) : Command;

public sealed class DeleteStrategyCommandHandler : CommandHandler<DeleteStrategyCommand>
{
    private readonly IStrategyRepository _repository;

    public DeleteStrategyCommandHandler(IStrategyRepository repository)
    {
        _repository = repository;
    }

    public override async Task<Unit> Handle(DeleteStrategyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Identity);

        var strategy = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (strategy is null || strategy.UserId != request.Identity.UserId || !strategy.IsActive)
        {
            throw new NotFoundException(nameof(Strategy), request.Id);
        }

        strategy.SoftDelete();
        await _repository.UpdateAsync(strategy, cancellationToken);
        return Unit.Value;
    }
}