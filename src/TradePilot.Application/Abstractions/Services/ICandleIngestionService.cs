using TradePilot.Application.Candles.Models;

namespace TradePilot.Application.Abstractions.Services;

public interface ICandleIngestionService
{
    Exchange Exchange { get; }

    Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default);
}