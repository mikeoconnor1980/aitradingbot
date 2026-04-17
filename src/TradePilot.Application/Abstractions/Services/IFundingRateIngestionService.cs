using TradePilot.Application.FundingRates.Models;

namespace TradePilot.Application.Abstractions.Services;

public interface IFundingRateIngestionService
{
    Task<FundingRateIngestionResult> IngestAsync(
        FundingRateIngestionRequest request,
        CancellationToken cancellationToken = default);
}