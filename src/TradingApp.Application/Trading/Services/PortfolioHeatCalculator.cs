using TradingApp.Application.MarketData.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Trading.Services;

/// <summary>
/// Stateless calculator for portfolio heat (aggregate risk across open positions).
/// </summary>
public static class PortfolioHeatCalculator
{
    /// <summary>
    /// Calculates portfolio heat from live exchange positions.
    /// </summary>
    public static PortfolioHeatResult CalculateFromPositions(
        IReadOnlyList<PositionDto> positions,
        decimal equity,
        decimal maxHeatPercent)
    {
        if (positions.Count == 0 || equity <= 0m)
        {
            return PortfolioHeatResult.Empty(maxHeatPercent) with
            {
                Equity = Math.Max(0m, equity)
            };
        }

        var entries = new List<PortfolioHeatEntry>(positions.Count);
        var totalRiskUsd = 0m;

        foreach (var position in positions)
        {
            var riskUsd = EstimatePositionRisk(position);
            if (riskUsd <= 0m)
            {
                continue;
            }

            var riskPercent = (riskUsd / equity) * 100m;
            totalRiskUsd += riskUsd;

            entries.Add(new PortfolioHeatEntry
            {
                Symbol = position.Asset,
                RiskUsd = riskUsd,
                RiskPercent = riskPercent
            });
        }

        return new PortfolioHeatResult
        {
            HeatPercent = CalculateHeatPercent(entries.Select(entry => entry.RiskUsd), equity),
            HeatUsd = totalRiskUsd,
            MaxHeatPercent = maxHeatPercent,
            Equity = equity,
            Entries = entries
        };
    }

    /// <summary>
    /// Calculates portfolio heat from tracked position risks keyed by symbol.
    /// </summary>
    public static PortfolioHeatResult CalculateFromTrackedRisks(
        IReadOnlyDictionary<string, decimal> trackedRisksUsd,
        decimal equity,
        decimal maxHeatPercent)
    {
        ArgumentNullException.ThrowIfNull(trackedRisksUsd);

        if (trackedRisksUsd.Count == 0 || equity <= 0m)
        {
            return PortfolioHeatResult.Empty(maxHeatPercent) with
            {
                Equity = Math.Max(0m, equity)
            };
        }

        var entries = trackedRisksUsd
            .Where(pair => pair.Value > 0m)
            .Select(pair => new PortfolioHeatEntry
            {
                Symbol = pair.Key,
                RiskUsd = pair.Value,
                RiskPercent = (pair.Value / equity) * 100m
            })
            .ToArray();

        var totalRiskUsd = entries.Sum(entry => entry.RiskUsd);

        return new PortfolioHeatResult
        {
            HeatPercent = CalculateHeatPercent(entries.Select(entry => entry.RiskUsd), equity),
            HeatUsd = totalRiskUsd,
            MaxHeatPercent = maxHeatPercent,
            Equity = equity,
            Entries = entries
        };
    }

    /// <summary>
    /// Calculates the aggregate heat percent for a set of risk amounts.
    /// </summary>
    public static decimal CalculateHeatPercent(IEnumerable<decimal> positionRisksUsd, decimal equity)
    {
        ArgumentNullException.ThrowIfNull(positionRisksUsd);

        if (equity <= 0m)
        {
            return 0m;
        }

        var totalRiskUsd = 0m;
        foreach (var riskUsd in positionRisksUsd)
        {
            if (riskUsd > 0m)
            {
                totalRiskUsd += riskUsd;
            }
        }

        return (totalRiskUsd / equity) * 100m;
    }

    /// <summary>
    /// Estimates the risk (R) in USD for an open position from exchange data.
    /// If stop-loss is set: R = abs(SL - entry) x abs(size)
    /// If no stop-loss: R = marginUsed (conservative proxy)
    /// </summary>
    public static decimal EstimatePositionRisk(PositionDto position)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (position.StopLossPrice.HasValue && position.StopLossPrice.Value > 0m)
        {
            return Math.Abs(position.StopLossPrice.Value - position.EntryPrice) * Math.Abs(position.Size);
        }

        return Math.Abs(position.MarginUsed);
    }
}