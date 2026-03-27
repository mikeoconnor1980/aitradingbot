using TradingApp.Domain.Entities;

namespace TradingApp.Application.Abstractions.Repositories;

public interface ICandleRepository
{
    Task<IReadOnlyList<Candle>> GetCandlesAsync(
        string symbol,
        string interval,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default);

    Task BulkInsertAsync(
        IEnumerable<Candle> candles,
        CancellationToken cancellationToken = default);

    Task<long?> GetLatestTimestampAsync(
        string symbol,
        string interval,
        CancellationToken cancellationToken = default);
}
