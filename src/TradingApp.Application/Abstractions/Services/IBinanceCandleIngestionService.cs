using TradingApp.Application.Candles.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface IBinanceCandleIngestionService
{
    Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default);
}