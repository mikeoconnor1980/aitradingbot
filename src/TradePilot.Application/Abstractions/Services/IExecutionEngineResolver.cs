namespace TradePilot.Application.Abstractions.Services;

public interface IExecutionEngineResolver
{
    IExecutionEngine Resolve(Exchange exchange);
}