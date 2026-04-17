namespace TradePilot.Application.Trading.Models;

/// <summary>
/// API response for the portfolio heat endpoint.
/// </summary>
public sealed class PortfolioHeatResponse
{
    public decimal HeatPercent { get; init; }

    public decimal MaxHeatPercent { get; init; }

    public decimal Equity { get; init; }

    public IReadOnlyList<PortfolioHeatPositionResponse> Positions { get; init; } = [];

    public static PortfolioHeatResponse Empty(decimal maxHeatPercent = 0m) => new()
    {
        MaxHeatPercent = maxHeatPercent
    };
}

/// <summary>
/// Risk contribution of a single position in the portfolio heat response.
/// </summary>
public sealed class PortfolioHeatPositionResponse
{
    public string Symbol { get; init; } = string.Empty;

    public decimal RiskUsd { get; init; }

    public decimal RiskPercent { get; init; }
}