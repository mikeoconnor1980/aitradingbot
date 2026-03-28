using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Candles.Models;

namespace TradingApp.Application.Candles.Commands;

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