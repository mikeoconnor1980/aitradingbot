using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.TradeJournal.Models;

/// <summary>Bounded filters for persisted logical trades.</summary>
public sealed record TradeJournalFilter(
    string UserId,
    Guid? StrategyId = null,
    int? StrategyVersion = null,
    string? Symbol = null,
    TradeSide? Side = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    TradeOutcome? Outcome = null,
    bool ClosedOnly = true);

/// <summary>Result returned by a bounded newest-first trade query.</summary>
public sealed record TradesResult(IReadOnlyList<TradeJournalRecord> Trades, int Limit);

/// <summary>A deterministically selected best or worst trade.</summary>
public sealed record TradeExtremum(Guid TradeId, string Symbol, decimal NetPnl, DateTime ExitTimeUtc);

/// <summary>Database-calculated facts for a completed-trade set.</summary>
public sealed record TradeAnalytics(
    int TradeCount,
    int WinningTrades,
    int LosingTrades,
    int BreakevenTrades,
    decimal GrossPnl,
    decimal NetPnl,
    decimal Fees,
    decimal? Funding,
    bool FundingComplete,
    decimal WinRate,
    decimal? AverageWin,
    decimal? AverageLoss,
    decimal AverageNetPnlPerTrade,
    decimal? ProfitFactor,
    bool ProfitFactorHasZeroLossDenominator,
    TimeSpan? AverageDuration,
    decimal? AverageMfeAmount,
    decimal? AverageMfePercent,
    decimal? AverageMaeAmount,
    decimal? AverageMaePercent,
    TradeExtremum? BestTrade,
    TradeExtremum? WorstTrade);

/// <summary>One deterministic strategy-version or regime analytics group.</summary>
public sealed record TradeAnalyticsGroup(string Key, TradeAnalytics Analytics);

/// <summary>Deterministic comparisons over strategy versions and recorded entry regimes.</summary>
public sealed record StrategyTradeAnalytics(
    IReadOnlyList<TradeAnalyticsGroup> ByStrategyVersion,
    IReadOnlyList<TradeAnalyticsGroup> ByEntryMarketRegime);
