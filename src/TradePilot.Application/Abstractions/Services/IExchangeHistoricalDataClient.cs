using TradePilot.Application.FundingRates.Models;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Application.Abstractions.Services;

public interface IExchangeHistoricalDataClient
{
    Exchange Exchange { get; }

    Task<IReadOnlyList<CandleSnapshotDto>> GetCandleSnapshotsAsync(
        TradingPair pair,
        string timeframe,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FundingRateDto>> GetFundingRatesAsync(
        TradingPair pair,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default);
}