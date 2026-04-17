using TradePilot.Application.Candles.Models;

namespace TradePilot.Application.Abstractions.Services;

public interface IBinanceCandleIngestionService
{
    Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default);
}