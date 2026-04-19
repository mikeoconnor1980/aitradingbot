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

    public Task<IReadOnlyList<CandleSnapshotDto>> GetCandleSnapshotsAsync(
        TradingPair pair,
        string timeframe,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pair);

        return _restClient.GetCandleSnapshotsAsync(pair.Base, timeframe, startTime, endTime, cancellationToken)
            .ContinueWith(static task => (IReadOnlyList<CandleSnapshotDto>)task.Result, cancellationToken);
    }

    public Task<IReadOnlyList<FundingRateDto>> GetFundingRatesAsync(
        TradingPair pair,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pair);

        throw new NotSupportedException("Hyperliquid funding-rate history is not implemented behind IExchangeHistoricalDataClient.");
    }
}