using TradingApp.Application.Candles.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface ICandleIngestionService
{
    Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default);
}