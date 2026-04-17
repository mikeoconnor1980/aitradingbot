using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.MarketData.Queries;

public sealed record GetMarketInfoQuery(string Asset) : Query<MarketInfoDto>;

public sealed class GetMarketInfoQueryHandler : QueryHandler<GetMarketInfoQuery, MarketInfoDto>
{
    private readonly IHyperliquidRestClient _restClient;

    public GetMarketInfoQueryHandler(IHyperliquidRestClient restClient)
    {
        _restClient = restClient;
    }

    public override async Task<MarketInfoDto> Handle(GetMarketInfoQuery request, CancellationToken cancellationToken)
    {
        var result = await _restClient.GetMarketInfoAsync(request.Asset, cancellationToken);

        if (result is null)
        {
            throw new NotFoundException("Asset", request.Asset);
        }

        return result;
    }
}