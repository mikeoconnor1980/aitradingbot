using System.Text;
using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;

namespace TradePilot.Persistence.Repositories;

public sealed class FundingRateRepository : IFundingRateRepository
{
    private const int BatchSize = 200; // 4 params/row × 200 = 800 (SQL Server limit: 2100)
    private readonly TradePilotDbContext _context;

    public FundingRateRepository(TradePilotDbContext context)
    {
        _context = context;
    }

    public async Task BulkInsertAsync(IEnumerable<FundingRate> fundingRates, CancellationToken cancellationToken = default)
    {
        var isInMemory = _context.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;

        foreach (var batch in fundingRates.Chunk(BatchSize))
        {
            if (isInMemory)
            {
                foreach (var rate in batch)
                {
                    var exists = await _context.FundingRates.AnyAsync(
                        f => f.Symbol == rate.Symbol && f.Timestamp == rate.Timestamp,
                        cancellationToken);
                    if (!exists)
                        _context.FundingRates.Add(rate);
                }
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                await BulkInsertSqlServerAsync(batch, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }
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