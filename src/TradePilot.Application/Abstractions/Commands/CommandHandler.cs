using MediatR;

namespace TradePilot.Application.Abstractions.Commands;

public abstract class CommandHandler<TCommand> : IRequestHandler<TCommand, Unit>
    where TCommand : Command
{
    public abstract Task<Unit> Handle(TCommand request, CancellationToken cancellationToken);
}

public abstract class CommandHandler<TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : Command<TResult>
{
    public abstract Task<TResult> Handle(TCommand request, CancellationToken cancellationToken);
}

public abstract class CreateCommandHandler<TCommand> : IRequestHandler<TCommand, Guid>
    where TCommand : CreateCommand
{
    public abstract Task<Guid> Handle(TCommand request, CancellationToken cancellationToken);
}