namespace TradePilot.Application.Abstractions.Services;

public interface IExecutionEngineResolver
{
    IExecutionEngine Resolve(Exchange exchange);

    IExecutionEngine Resolve(Exchange exchange, AssetType assetType);
}