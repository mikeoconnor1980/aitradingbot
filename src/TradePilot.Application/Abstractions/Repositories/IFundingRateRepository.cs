using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IFundingRateRepository
{
    Task BulkInsertAsync(IEnumerable<FundingRate> fundingRates, CancellationToken cancellationToken = default);

    Task<long?> GetLatestTimestampAsync(string symbol, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FundingRate>> GetRangeAsync(
        string symbol,
        long startTimestamp,
        long endTimestamp,
        CancellationToken cancellationToken = default);
}