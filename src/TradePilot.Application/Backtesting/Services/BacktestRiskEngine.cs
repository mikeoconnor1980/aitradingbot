using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;

namespace TradePilot.Application.Backtesting.Services;

/// <summary>
/// Risk engine for backtesting that enforces portfolio heat and drawdown limits.
/// </summary>
public sealed class BacktestRiskEngine : IRiskEngine
{
    private readonly RiskLimitsConfig _limits;
    private readonly IReadOnlyList<DrawdownTier> _drawdownTiers;
    private readonly ConcurrentDictionary<string, decimal> _positionRisks = new(StringComparer.OrdinalIgnoreCase);
    private decimal _accountEquity;
    private decimal _highWaterMark;
    private decimal _drawdownScalingFactor = 1.0m;
    private volatile bool _drawdownCircuitBreakerTripped;
    private int _heatBlockedSignalCount;
    private int _drawdownBlockedSignalCount;

    public BacktestRiskEngine(IOptions<RiskLimitsConfig> limits)
    {
        _limits = limits?.Value ?? throw new ArgumentNullException(nameof(limits));
        _drawdownTiers = _limits.DrawdownTiers;
    }

    public int HeatBlockedSignalCount => _heatBlockedSignalCount;

    public int DrawdownBlockedSignalCount => _drawdownBlockedSignalCount;

    public decimal DrawdownScalingFactor => _drawdownScalingFactor;

    public bool IsDrawdownCircuitBreakerTripped => _drawdownCircuitBreakerTripped;

    public Task<IReadOnlyList<TradingSignal>> ValidateAsync(
        IReadOnlyList<TradingSignal> signals,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(signals);

        if (signals.Count == 0)
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

            if (_drawdownCircuitBreakerTripped)
            {
                Interlocked.Increment(ref _drawdownBlockedSignalCount);
                continue;
            }

            if (_limits.MaxPortfolioHeatPercent > 0m && !CheckPortfolioHeat(signal))
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

        if (_highWaterMark == 0m)
        {
            _highWaterMark = _accountEquity;
        }

        var drawdownResult = DrawdownEvaluator.Evaluate(
            _accountEquity,
            _highWaterMark,
            _drawdownTiers);

        _highWaterMark = drawdownResult.NewHighWaterMark;
        UpdateDrawdownState(drawdownResult.ScalingFactor, drawdownResult.IsHalted);
    }

    public void UpdateDrawdownState(decimal scalingFactor, bool isHalted)
    {
        _drawdownScalingFactor = Math.Max(0m, scalingFactor);
        _drawdownCircuitBreakerTripped = isHalted;
    }

    public void Reset()
    {
        _positionRisks.Clear();
        _accountEquity = 0m;
        _highWaterMark = 0m;
        _drawdownScalingFactor = 1.0m;
        _drawdownCircuitBreakerTripped = false;
        _heatBlockedSignalCount = 0;
        _drawdownBlockedSignalCount = 0;
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

        try
        {
            riskUsd = Convert.ToDecimal(estimatedRisk);
            return riskUsd > 0m;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }
}