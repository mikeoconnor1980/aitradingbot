using TradePilot.Application.Abstractions.Commands;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Candles.Models;

namespace TradePilot.Application.Candles.Commands;

public sealed record IngestBinanceCandlesCommand(IngestionRequest Request) : Command<IngestionResult>;

public sealed class IngestBinanceCandlesCommandHandler
    : CommandHandler<IngestBinanceCandlesCommand, IngestionResult>
{
    private readonly IBinanceCandleIngestionService _ingestionService;

    public IngestBinanceCandlesCommandHandler(IBinanceCandleIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    public override async Task<IngestionResult> Handle(
        IngestBinanceCandlesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Request);

        return await _ingestionService.IngestAsync(request.Request, cancellationToken);
    }
}