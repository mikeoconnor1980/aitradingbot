using TradePilot.Application.Abstractions.Commands;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.StrategyAuthoring.Commands;

public sealed record InterpretStrategyCommand(string UserText) : Command<StrategyIntentDto>;

public sealed class InterpretStrategyCommandHandler
    : CommandHandler<InterpretStrategyCommand, StrategyIntentDto>
{
    private readonly IStrategyInterpreter _interpreter;

    public InterpretStrategyCommandHandler(IStrategyInterpreter interpreter)
    {
        _interpreter = interpreter;
    }

    public override async Task<StrategyIntentDto> Handle(
        InterpretStrategyCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserText);

        return await _interpreter.InterpretAsync(request.UserText, cancellationToken);
    }
}