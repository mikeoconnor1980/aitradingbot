namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Result of a portfolio heat calculation.
/// </summary>
public sealed record PortfolioHeatResult
{
    public required decimal HeatPercent { get; init; }

    public required decimal HeatUsd { get; init; }

    public required decimal MaxHeatPercent { get; init; }

    public required decimal Equity { get; init; }

    public required IReadOnlyList<PortfolioHeatEntry> Entries { get; init; }

    public bool IsLimitExceeded => MaxHeatPercent > 0m && HeatPercent > MaxHeatPercent;

    public bool IsLimitEnabled => MaxHeatPercent > 0m;

    public static PortfolioHeatResult Empty(decimal maxHeatPercent = 0m) => new()
    {
        HeatPercent = 0m,
        HeatUsd = 0m,
        MaxHeatPercent = maxHeatPercent,
        Equity = 0m,
        Entries = []
    };
}