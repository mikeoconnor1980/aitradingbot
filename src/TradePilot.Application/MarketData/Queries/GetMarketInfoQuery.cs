using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Application.MarketData.Queries;

public sealed record GetMarketInfoQuery(string Asset) : Query<MarketInfoDto>;

public sealed class GetMarketInfoQueryHandler : QueryHandler<GetMarketInfoQuery, MarketInfoDto>
{
    private readonly IExchangeMarketMetadataProvider _marketMetadataProvider;
    private readonly IExchangeSymbolMapper _symbolMapper;

    public GetMarketInfoQueryHandler(
        IEnumerable<IExchangeMarketMetadataProvider> marketMetadataProviders,
        IEnumerable<IExchangeSymbolMapper> symbolMappers)
    {
        _marketMetadataProvider = ResolveProvider(marketMetadataProviders, Exchange.Hyperliquid);
        _symbolMapper = ResolveSymbolMapper(symbolMappers, Exchange.Hyperliquid);
    }

    public override async Task<MarketInfoDto> Handle(GetMarketInfoQuery request, CancellationToken cancellationToken)
    {
        var pair = _symbolMapper.FromExchangeSymbol(request.Asset);
        var result = await _marketMetadataProvider.GetMarketInfoAsync(pair, cancellationToken);

        if (result is null)
        {
            throw new NotFoundException("Asset", request.Asset);
        }

        return result;
    }

    private static IExchangeMarketMetadataProvider ResolveProvider(
        IEnumerable<IExchangeMarketMetadataProvider> providers,
        Exchange exchange)
    {
        return providers.FirstOrDefault(provider => provider.Exchange == exchange)
            ?? throw new InvalidOperationException($"No market metadata provider is registered for exchange '{exchange}'.");
    }

    private static IExchangeSymbolMapper ResolveSymbolMapper(
        IEnumerable<IExchangeSymbolMapper> symbolMappers,
        Exchange exchange)
    {
        return symbolMappers.FirstOrDefault(mapper => mapper.Exchange == exchange)
            ?? throw new InvalidOperationException($"No symbol mapper is registered for exchange '{exchange}'.");
    }
}