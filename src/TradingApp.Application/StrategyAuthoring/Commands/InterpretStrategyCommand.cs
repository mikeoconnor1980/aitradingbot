using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.StrategyAuthoring.Commands;

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

        return await _interpreter.InterpretAsync(request.UserText, cancellationToken);
    }
}