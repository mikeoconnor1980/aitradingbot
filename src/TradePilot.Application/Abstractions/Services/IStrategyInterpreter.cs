using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.Abstractions.Services;

public interface IStrategyInterpreter
{
    Task<StrategyIntentDto> InterpretAsync(string userText, CancellationToken cancellationToken);
}