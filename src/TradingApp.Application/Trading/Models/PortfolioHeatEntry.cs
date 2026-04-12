namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Represents the risk contribution of a single open position to portfolio heat.
/// </summary>
public sealed record PortfolioHeatEntry
{
    public required string Symbol { get; init; }

    public required decimal RiskUsd { get; init; }

    public required decimal RiskPercent { get; init; }
}