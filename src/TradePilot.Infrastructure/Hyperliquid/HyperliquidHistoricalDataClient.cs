using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.FundingRates.Models;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Hyperliquid;

public sealed class HyperliquidHistoricalDataClient : IExchangeHistoricalDataClient
{
    private readonly IHyperliquidRestClient _restClient;

    public HyperliquidHistoricalDataClient(IHyperliquidRestClient restClient)
    {
        _restClient = restClient;
    }

    public Exchange Exchange => Exchange.Hyperliquid;

    public async Task<IReadOnlyList<CandleSnapshotDto>> GetCandleSnapshotsAsync(
        TradingPair pair,
        string timeframe,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pair);

        var result = await _restClient.GetCandleSnapshotsAsync(pair.Base, timeframe, startTime, endTime, cancellationToken);
        return result;
    }

    public Task<IReadOnlyList<FundingRateDto>> GetFundingRatesAsync(
        TradingPair pair,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pair);

        return Task.FromResult<IReadOnlyList<FundingRateDto>>(Array.Empty<FundingRateDto>());
    }
}