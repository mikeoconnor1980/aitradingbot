using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Candles.Models;

namespace TradingApp.Application.Candles.Commands;

public sealed record IngestCandlesCommand(
    string Symbol,
    string[] Intervals,
    long? StartTime,
    long? EndTime) : Command<IngestionResult>;

public sealed class IngestCandlesCommandHandler : CommandHandler<IngestCandlesCommand, IngestionResult>
{
    private readonly ICandleIngestionService _ingestionService;

    public IngestCandlesCommandHandler(ICandleIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    public override async Task<IngestionResult> Handle(IngestCandlesCommand request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Symbol);
        ArgumentNullException.ThrowIfNull(request.Intervals);

        return await _ingestionService.IngestAsync(
            new IngestionRequest
            {
                Symbol = request.Symbol,
                Intervals = request.Intervals,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
            },
            cancellationToken);
    }
}