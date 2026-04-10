using System.Text;
using Microsoft.EntityFrameworkCore;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;

namespace TradingApp.Persistence.Repositories;

public sealed class FundingRateRepository : IFundingRateRepository
{
    private const int SqliteBatchSize = 500;
    private const int SqlServerBatchSize = 200; // 4 params/row × 200 = 800 (SQL Server limit: 2100)
    private readonly TradingAppDbContext _context;

    public FundingRateRepository(TradingAppDbContext context)
    {
        _context = context;
    }

    public async Task BulkInsertAsync(IEnumerable<FundingRate> fundingRates, CancellationToken cancellationToken = default)
    {
        var isSqlServer = _context.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;
        var batchSize = isSqlServer ? SqlServerBatchSize : SqliteBatchSize;

        foreach (var batch in fundingRates.Chunk(batchSize))
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

    private async Task BulkInsertSqliteAsync(FundingRate[] batch, CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();
        sql.Append("INSERT OR IGNORE INTO FundingRates (Symbol, Timestamp, Rate, MarkPrice) VALUES ");

        var parameters = new List<object>();
        for (var i = 0; i < batch.Length; i++)
        {
            if (i > 0)
            {
                sql.Append(',');
            }

            var offset = i * 4;
            sql.Append($"({{{offset}}},{{{offset + 1}}},{{{offset + 2}}},{{{offset + 3}}})");

            var fundingRate = batch[i];
            parameters.Add(fundingRate.Symbol);
            parameters.Add(fundingRate.Timestamp);
            parameters.Add((double)fundingRate.Rate);
            parameters.Add((double)fundingRate.MarkPrice);
        }

        await _context.Database.ExecuteSqlRawAsync(sql.ToString(), parameters, cancellationToken);
    }

    private async Task BulkInsertSqlServerAsync(FundingRate[] batch, CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();
        sql.Append(
            """
            MERGE INTO FundingRates AS target
            USING (VALUES 
            """);

        var parameters = new List<object>();
        for (var i = 0; i < batch.Length; i++)
        {
            if (i > 0)
            {
                sql.Append(',');
            }

            var offset = i * 4;
            sql.Append($"({{{offset}}},{{{offset + 1}}},{{{offset + 2}}},{{{offset + 3}}})");

            var fundingRate = batch[i];
            parameters.Add(fundingRate.Symbol);
            parameters.Add(fundingRate.Timestamp);
            parameters.Add((double)fundingRate.Rate);
            parameters.Add((double)fundingRate.MarkPrice);
        }

        sql.Append(
            """
            ) AS source (Symbol, [Timestamp], Rate, MarkPrice)
            ON target.Symbol = source.Symbol AND target.[Timestamp] = source.[Timestamp]
            WHEN NOT MATCHED THEN
                INSERT (Symbol, [Timestamp], Rate, MarkPrice)
                VALUES (source.Symbol, source.[Timestamp], source.Rate, source.MarkPrice);
            """);

        await _context.Database.ExecuteSqlRawAsync(sql.ToString(), parameters, cancellationToken);
    }

    public async Task<long?> GetLatestTimestampAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        return await _context.FundingRates
            .Where(fundingRate => fundingRate.Symbol == symbol)
            .MaxAsync(fundingRate => (long?)fundingRate.Timestamp, cancellationToken);
    }

    public async Task<IReadOnlyList<FundingRate>> GetRangeAsync(
        string symbol,
        long startTimestamp,
        long endTimestamp,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startTimestamp);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startTimestamp, endTimestamp);

        return await _context.FundingRates
            .Where(fundingRate => fundingRate.Symbol == symbol)
            .Where(fundingRate => fundingRate.Timestamp >= startTimestamp && fundingRate.Timestamp <= endTimestamp)
            .OrderBy(fundingRate => fundingRate.Timestamp)
            .ToListAsync(cancellationToken);
    }
}