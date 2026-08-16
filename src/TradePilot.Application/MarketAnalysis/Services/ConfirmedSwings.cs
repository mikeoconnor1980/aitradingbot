namespace TradePilot.Application.MarketAnalysis.Services;

internal sealed record ConfirmedSwings(
    IReadOnlyList<decimal> Highs,
    IReadOnlyList<decimal> Lows);
