using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.FundingRates.Models;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceHistoricalDataClient : IExchangeHistoricalDataClient
{
    private readonly IBinanceFuturesRestClient _restClient;

    public BinanceHistoricalDataClient(IBinanceFuturesRestClient restClient)
    {
        _restClient = restClient;
    }

    public Exchange Exchange => Exchange.Binance;

    public Task<IReadOnlyList<CandleSnapshotDto>> GetCandleSnapshotsAsync(
        TradingPair pair,
        string timeframe,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pair);

        return _restClient.GetKlinesAsync(
            BinanceAssetMapper.ToFuturesSymbol(pair.Base),
            timeframe,
            startTime,
            endTime,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<FundingRateDto>> GetFundingRatesAsync(
        TradingPair pair,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pair);

        return _restClient.GetFundingRatesAsync(
            BinanceAssetMapper.ToFuturesSymbol(pair.Base),
            startTime,
            endTime,
            cancellationToken: cancellationToken);
    }
}