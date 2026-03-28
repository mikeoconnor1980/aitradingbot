using TradingApp.Application.MarketData.Models;
using TradingApp.Application.FundingRates.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface IBinanceFuturesRestClient
{
    Task<IReadOnlyList<CandleSnapshotDto>> GetKlinesAsync(
        string futuresSymbol,
        string interval,
        long startTime,
        long? endTime = null,
        int limit = 1500,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CandleSnapshotDto>> GetMarkPriceKlinesAsync(
        string futuresSymbol,
        string interval,
        long startTime,
        long? endTime = null,
        int limit = 1500,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FundingRateDto>> GetFundingRatesAsync(
        string futuresSymbol,
        long startTime,
        long? endTime = null,
        int limit = 1000,
        CancellationToken cancellationToken = default);
}