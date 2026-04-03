using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface IStrategyInterpreter
{
    Task<StrategyIntentDto> InterpretAsync(string userText, CancellationToken cancellationToken);
}