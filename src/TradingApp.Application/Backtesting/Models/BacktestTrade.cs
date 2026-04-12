using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Enums;

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
    public string? ExitReason { get; init; }

    /// <summary>Dollar risk (1R) at trade entry. Null for non-RiskBased sizing.</summary>
    public decimal? InitialRDollars { get; init; }

    /// <summary>Realised return expressed as a multiple of R (PnL / InitialR). Null if InitialR is not tracked.</summary>
    public decimal? RMultipleResult { get; init; }

    /// <summary>Maximum favourable excursion in R multiples (best unrealised profit / InitialR). Always >= 0.</summary>
    public decimal? MFE { get; init; }

    /// <summary>Maximum adverse excursion in R multiples (worst unrealised loss / InitialR). Always <= 0.</summary>
    public decimal? MAE { get; init; }
}
