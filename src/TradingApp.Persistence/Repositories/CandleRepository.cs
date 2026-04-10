using System.Text;
using Microsoft.EntityFrameworkCore;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;

namespace TradingApp.Persistence.Repositories;

public sealed class CandleRepository : ICandleRepository
{
    private const int SqliteBatchSize = 500;
    private const int SqlServerBatchSize = 200; // 10 params/row × 200 = 2000 (SQL Server limit: 2100)
    private readonly TradingAppDbContext _context;

    public CandleRepository(TradingAppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Candle>> GetCandlesAsync(
        string symbol,
        string interval,
        long startTime,
        long endTime,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);

        var query = _context.Candles
            .Where(c => c.Symbol == symbol
                && c.Interval == interval
                && c.Timestamp >= startTime
                && c.Timestamp <= endTime);

        if (source is not null)
        {
            query = query.Where(c => c.Source == source);
        }

        return await query
            .OrderBy(c => c.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task BulkInsertAsync(
        IEnumerable<Candle> candles,
        CancellationToken cancellationToken = default)
    {
        var isSqlServer = _context.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;
        var batchSize = isSqlServer ? SqlServerBatchSize : SqliteBatchSize;

        foreach (var batch in candles.Chunk(batchSize))
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            if (isSqlServer)
            {
                await BulkInsertSqlServerAsync(batch, cancellationToken);
            }
            else
            {
                await BulkInsertSqliteAsync(batch, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }

    private async Task BulkInsertSqliteAsync(Candle[] batch, CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();
        sql.Append("INSERT OR IGNORE INTO Candles (Source, Symbol, Interval, Timestamp, Open, High, Low, Close, Volume, NumTrades) VALUES ");

        var parameters = new List<object>();
        for (var i = 0; i < batch.Length; i++)
        {
            if (i > 0)
            {
                sql.Append(',');
            }

            var offset = i * 10;
            sql.Append($"({{{offset}}},{{{offset + 1}}},{{{offset + 2}}},{{{offset + 3}}},{{{offset + 4}}},{{{offset + 5}}},{{{offset + 6}}},{{{offset + 7}}},{{{offset + 8}}},{{{offset + 9}}})");

            var candle = batch[i];
            parameters.Add(candle.Source);
            parameters.Add(candle.Symbol);
            parameters.Add(candle.Interval);
            parameters.Add(candle.Timestamp);
            parameters.Add((double)candle.Open);
            parameters.Add((double)candle.High);
            parameters.Add((double)candle.Low);
            parameters.Add((double)candle.Close);
            parameters.Add((double)candle.Volume);
            parameters.Add(candle.NumTrades);
        }

        await _context.Database.ExecuteSqlRawAsync(sql.ToString(), parameters, cancellationToken);
    }

    private async Task BulkInsertSqlServerAsync(Candle[] batch, CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();
        sql.Append(
            """
            MERGE INTO Candles AS target
            USING (VALUES 
            """);

        var parameters = new List<object>();
        for (var i = 0; i < batch.Length; i++)
        {
            if (i > 0)
            {
                sql.Append(',');
            }

            var offset = i * 10;
            sql.Append($"({{{offset}}},{{{offset + 1}}},{{{offset + 2}}},{{{offset + 3}}},{{{offset + 4}}},{{{offset + 5}}},{{{offset + 6}}},{{{offset + 7}}},{{{offset + 8}}},{{{offset + 9}}})");

            var candle = batch[i];
            parameters.Add(candle.Source);
            parameters.Add(candle.Symbol);
            parameters.Add(candle.Interval);
            parameters.Add(candle.Timestamp);
            parameters.Add((double)candle.Open);
            parameters.Add((double)candle.High);
            parameters.Add((double)candle.Low);
            parameters.Add((double)candle.Close);
            parameters.Add((double)candle.Volume);
            parameters.Add(candle.NumTrades);
        }

        sql.Append(
            """
            ) AS source (Source, Symbol, [Interval], [Timestamp], [Open], High, Low, [Close], Volume, NumTrades)
            ON target.Source = source.Source AND target.Symbol = source.Symbol AND target.[Interval] = source.[Interval] AND target.[Timestamp] = source.[Timestamp]
            WHEN NOT MATCHED THEN
                INSERT (Source, Symbol, [Interval], [Timestamp], [Open], High, Low, [Close], Volume, NumTrades)
                VALUES (source.Source, source.Symbol, source.[Interval], source.[Timestamp], source.[Open], source.High, source.Low, source.[Close], source.Volume, source.NumTrades);
            """);

        await _context.Database.ExecuteSqlRawAsync(sql.ToString(), parameters, cancellationToken);
    }

    public async Task<long?> GetLatestTimestampAsync(
        string symbol,
        string interval,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);

        var query = _context.Candles
            .Where(c => c.Symbol == symbol && c.Interval == interval);

        if (source is not null)
        {
            query = query.Where(c => c.Source == source);
        }

        return await query
            .MaxAsync(c => (long?)c.Timestamp, cancellationToken);
    }

    public async Task<(long? FromTimestampUtc, long? ToTimestampUtc, int CandleCount)> GetCoverageAsync(
        string symbol,
        string interval,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);

        var query = _context.Candles
            .Where(c => c.Symbol == symbol && c.Interval == interval);

        if (source is not null)
        {
            query = query.Where(c => c.Source == source);
        }

        var coverage = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                FromTimestampUtc = (long?)group.Min(candle => candle.Timestamp),
                ToTimestampUtc = (long?)group.Max(candle => candle.Timestamp),
                CandleCount = group.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return coverage is null
            ? (null, null, 0)
            : (coverage.FromTimestampUtc, coverage.ToTimestampUtc, coverage.CandleCount);
    }
}
