using TradePilot.Application.Abstractions.Commands;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.FundingRates.Models;

namespace TradePilot.Application.FundingRates.Commands;

public sealed record IngestFundingRatesCommand(FundingRateIngestionRequest Request)
    : Command<FundingRateIngestionResult>;

public sealed class IngestFundingRatesCommandHandler
    : CommandHandler<IngestFundingRatesCommand, FundingRateIngestionResult>
{
    private readonly IFundingRateIngestionService _ingestionService;

    public IngestFundingRatesCommandHandler(IFundingRateIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    public override async Task<FundingRateIngestionResult> Handle(
        IngestFundingRatesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Request);

        return await _ingestionService.IngestAsync(request.Request, cancellationToken);
    }
}