using TradingApp.Application.FundingRates.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface IFundingRateIngestionService
{
    Task<FundingRateIngestionResult> IngestAsync(
        FundingRateIngestionRequest request,
        CancellationToken cancellationToken = default);
}