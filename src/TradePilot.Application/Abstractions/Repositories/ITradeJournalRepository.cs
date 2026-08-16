using TradePilot.Application.TradeJournal.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

/// <summary>Persistence boundary for logical live-trade history and database-side analytics.</summary>
public interface ITradeJournalRepository
{
    Task<TradeJournalRecord?> GetOpenAsync(
        string userId,
        string symbol,
        Guid? strategyId,
        CancellationToken cancellationToken = default);

    Task AddAsync(TradeJournalRecord trade, CancellationToken cancellationToken = default);
    Task UpdateAsync(TradeJournalRecord trade, CancellationToken cancellationToken = default);

    Task<TradeJournalRecord?> GetByIdAsync(
        Guid tradeId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradeJournalRecord>> GetAsync(
        TradeJournalFilter filter,
        int limit,
        CancellationToken cancellationToken = default);

    Task<TradeAnalytics> GetAnalyticsAsync(
        TradeJournalFilter filter,
        CancellationToken cancellationToken = default);

    Task<StrategyTradeAnalytics> GetStrategyAnalyticsAsync(
        TradeJournalFilter filter,
        CancellationToken cancellationToken = default);
}
