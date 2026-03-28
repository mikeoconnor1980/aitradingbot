using TradingApp.Domain.Entities;

namespace TradingApp.Application.Abstractions.Repositories;

public interface IFundingRateRepository
{
    Task BulkInsertAsync(IEnumerable<FundingRate> fundingRates, CancellationToken cancellationToken = default);

    Task<long?> GetLatestTimestampAsync(string symbol, CancellationToken cancellationToken = default);
}