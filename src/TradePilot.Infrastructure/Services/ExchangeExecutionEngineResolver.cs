using Microsoft.Extensions.DependencyInjection;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Infrastructure.Services;

public sealed class ExchangeExecutionEngineResolver : IExecutionEngineResolver
{
    private readonly IServiceProvider _serviceProvider;

    public ExchangeExecutionEngineResolver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IExecutionEngine Resolve(Exchange exchange)
        => _serviceProvider.GetRequiredKeyedService<IExecutionEngine>(exchange.ToString());

    public IExecutionEngine Resolve(Exchange exchange, AssetType assetType)
    {
        var compositeKey = $"{exchange}:{assetType}";
        var engine = _serviceProvider.GetKeyedService<IExecutionEngine>(compositeKey);
        return engine ?? _serviceProvider.GetRequiredKeyedService<IExecutionEngine>(exchange.ToString());
    }
}