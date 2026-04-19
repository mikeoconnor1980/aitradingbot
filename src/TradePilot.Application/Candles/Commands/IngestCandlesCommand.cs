using TradePilot.Application.Abstractions.Commands;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Candles.Models;

namespace TradePilot.Application.Candles.Commands;

public sealed record IngestCandlesCommand(
    string Symbol,
    string[] Intervals,
    long? StartTime,
    long? EndTime) : Command<IngestionResult>;

public sealed class IngestCandlesCommandHandler : CommandHandler<IngestCandlesCommand, IngestionResult>
{
    private readonly ICandleIngestionService _ingestionService;

    public IngestCandlesCommandHandler(IEnumerable<ICandleIngestionService> ingestionServices)
    {
        _ingestionService = ResolveIngestionService(ingestionServices, Exchange.Hyperliquid);
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

    private static ICandleIngestionService ResolveIngestionService(
        IEnumerable<ICandleIngestionService> ingestionServices,
        Exchange exchange)
    {
        var service = ingestionServices.FirstOrDefault(candidate => candidate.Exchange == exchange);
        return service
            ?? throw new InvalidOperationException($"No candle ingestion service is registered for exchange '{exchange}'.");
    }
}