using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Application.MarketData.Queries;

public sealed record GetCandlesQuery(string Asset, string Timeframe, long? EndTime = null) : Query<List<CandleDto>>;

public sealed class GetCandlesQueryHandler : QueryHandler<GetCandlesQuery, List<CandleDto>>
{
    private readonly IHyperliquidRestClient _restClient;

    public GetCandlesQueryHandler(IHyperliquidRestClient restClient)
    {
        _restClient = restClient;
    }

    public override async Task<List<CandleDto>> Handle(GetCandlesQuery request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Timeframe);

        return await _restClient.GetCandlesAsync(request.Asset, request.Timeframe, request.EndTime, cancellationToken);
    }
}