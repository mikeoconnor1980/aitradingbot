using System.Text;
using Microsoft.EntityFrameworkCore;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;

namespace TradingApp.Persistence.Repositories;

public sealed class FundingRateRepository : IFundingRateRepository
{
    private const int BatchSize = 500;
    private readonly TradingAppDbContext _context;

    public FundingRateRepository(TradingAppDbContext context)
    {
        _context = context;
    }

    public async Task BulkInsertAsync(IEnumerable<FundingRate> fundingRates, CancellationToken cancellationToken = default)
    {
        foreach (var batch in fundingRates.Chunk(BatchSize))
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

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

            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task<long?> GetLatestTimestampAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        return await _context.FundingRates
            .Where(fundingRate => fundingRate.Symbol == symbol)
            .MaxAsync(fundingRate => (long?)fundingRate.Timestamp, cancellationToken);
    }
}