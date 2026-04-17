using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface ICandleRepository
{
    Task<IReadOnlyList<Candle>> GetCandlesAsync(
        string symbol,
        string interval,
        long startTime,
        long endTime,
        string? source = null,
        CancellationToken cancellationToken = default);

    Task BulkInsertAsync(
        IEnumerable<Candle> candles,
        CancellationToken cancellationToken = default);

    Task<long?> GetLatestTimestampAsync(
        string symbol,
        string interval,
        string? source = null,
        CancellationToken cancellationToken = default);

    Task<(long? FromTimestampUtc, long? ToTimestampUtc, int CandleCount)> GetCoverageAsync(
        string symbol,
        string interval,
        string? source = null,
        CancellationToken cancellationToken = default);
}
