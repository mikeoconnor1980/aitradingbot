using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Backtesting.Models;

public sealed class BacktestTrade
{
    public required string TradeId { get; init; }
    public required string GridCycleId { get; init; }
    public required long EntryTimeUtc { get; init; }
    public required decimal EntryPrice { get; init; }
    public long? ExitTimeUtc { get; init; }
    public decimal? ExitPrice { get; init; }
    public required OrderSide Side { get; init; }
    public required decimal Size { get; init; }
    public decimal? PnL { get; init; }
    public required decimal Fees { get; init; }
    public required TradeType TradeType { get; init; }
}
