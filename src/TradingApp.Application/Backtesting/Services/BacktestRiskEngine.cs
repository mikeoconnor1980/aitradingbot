using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// Risk engine for backtesting that enforces portfolio heat limits only.
/// </summary>
public sealed class BacktestRiskEngine : IRiskEngine
{
    private readonly RiskLimitsConfig _limits;
    private readonly ConcurrentDictionary<string, decimal> _positionRisks = new(StringComparer.OrdinalIgnoreCase);
    private decimal _accountEquity;
    private int _heatBlockedSignalCount;

    public BacktestRiskEngine(IOptions<RiskLimitsConfig> limits)
    {
        _limits = limits?.Value ?? throw new ArgumentNullException(nameof(limits));
    }

    public int HeatBlockedSignalCount => _heatBlockedSignalCount;

    public Task<IReadOnlyList<TradingSignal>> ValidateAsync(
        IReadOnlyList<TradingSignal> signals,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(signals);

        if (signals.Count == 0 || _limits.MaxPortfolioHeatPercent <= 0m)
        {
            return Task.FromResult(signals);
        }

        var approvedSignals = new List<TradingSignal>(signals.Count);
        foreach (var signal in signals)
        {
            if (IsRiskReducing(signal))
            {
                approvedSignals.Add(signal);
                TrackPositionCloseFromSignal(signal);
                continue;
            }

            if (!CheckPortfolioHeat(signal))
            {
                Interlocked.Increment(ref _heatBlockedSignalCount);
                continue;
            }

            approvedSignals.Add(signal);
            TrackPositionOpenFromSignal(signal);
        }

        return Task.FromResult<IReadOnlyList<TradingSignal>>(approvedSignals);
    }

    public void UpdatePortfolioState(decimal accountEquity)
    {
        _accountEquity = Math.Max(0m, accountEquity);
    }

    public void RecordPositionOpened(string symbol, decimal riskUsd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        if (riskUsd <= 0m)
        {
            return;
        }

        _positionRisks[symbol] = riskUsd;
    }

    public void RecordPositionClosed(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        _positionRisks.TryRemove(symbol, out _);
    }

    private bool CheckPortfolioHeat(TradingSignal signal)
    {
        if (_accountEquity <= 0m)
        {
            return true;
        }

        if (!TryGetEstimatedRisk(signal, out var newTradeRiskUsd))
        {
            return true;
        }

        var currentHeatUsd = _positionRisks.Values.Sum();
        var maxHeatUsd = _accountEquity * (_limits.MaxPortfolioHeatPercent / 100m);

        return currentHeatUsd + newTradeRiskUsd <= maxHeatUsd;
    }

    private void TrackPositionOpenFromSignal(TradingSignal signal)
    {
        if (TryGetEstimatedRisk(signal, out var riskUsd))
        {
            RecordPositionOpened(signal.Symbol, riskUsd);
        }
    }

    private void TrackPositionCloseFromSignal(TradingSignal signal)
    {
        if (signal.SignalType is "TakeProfit" or "FlattenPosition" or "CloseHedge")
        {
            RecordPositionClosed(signal.Symbol);
        }
    }

    private static bool IsRiskReducing(TradingSignal signal)
    {
        return signal.SignalType is "TakeProfit" or "CancelGrid" or "FlattenPosition" or "CloseHedge";
    }

    private static bool TryGetEstimatedRisk(TradingSignal signal, out decimal riskUsd)
    {
        riskUsd = 0m;

        if (signal.Parameters is null
            || !signal.Parameters.TryGetValue("estimatedRiskUsd", out var estimatedRisk))
        {
            return false;
        }

        riskUsd = Convert.ToDecimal(estimatedRisk);
        return riskUsd > 0m;
    }
}