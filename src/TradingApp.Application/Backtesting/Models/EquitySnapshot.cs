namespace TradingApp.Application.Backtesting.Models;

public sealed record EquitySnapshot(long TimestampUtc, decimal Equity);
