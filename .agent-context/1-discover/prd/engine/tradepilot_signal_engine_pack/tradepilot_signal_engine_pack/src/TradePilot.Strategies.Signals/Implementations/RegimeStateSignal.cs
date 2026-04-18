using System;
using System.Collections.Generic;
using System.Linq;
using TradePilot.Strategies.Signals.Abstractions;
using TradePilot.Strategies.Signals.Models;

namespace TradePilot.Strategies.Signals.Implementations;

public sealed class RegimeStateSignal : IDerivedSignal
{
    public string Name => "regime_state";

    public SignalEvaluationResult Evaluate(ISignalContext context, SignalRequest request)
    {
        var candles = context.GetCandles(request.Timeframe);
        var lookbackBars = SignalParameterReader.GetInt(request.Parameters, "lookback_bars", 30);
        var breakoutThresholdPercent = SignalParameterReader.GetDecimal(request.Parameters, "breakout_threshold_percent", 2.5m);
        var highVolatilityAtrPercent = SignalParameterReader.GetDecimal(request.Parameters, "high_volatility_atr_percent", 2.0m);
        var parabolicThresholdPercent = SignalParameterReader.GetDecimal(request.Parameters, "parabolic_threshold_percent", 8.0m);

        if (candles.Count < lookbackBars)
            return SignalEvaluationResult.False("Not enough candles.");

        var window = candles[^lookbackBars..];
        var firstClose = window[0].Close;
        var lastClose = window[^1].Close;
        var avgRange = CandleMath.AverageRange(window, lookbackBars);

        if (firstClose == 0 || lastClose == 0)
            return SignalEvaluationResult.False("Invalid price series.");

        var trendPercent = ((lastClose - firstClose) / firstClose) * 100m;
        var atrPercent = (avgRange / lastClose) * 100m;

        var regime = Classify(trendPercent, atrPercent, breakoutThresholdPercent, highVolatilityAtrPercent, parabolicThresholdPercent);

        return SignalEvaluationResult.True(
            1m,
            new Dictionary<string, object?>
            {
                ["regime"] = regime.ToString(),
                ["trend_percent"] = trendPercent,
                ["atr_percent"] = atrPercent
            });
    }

    private static MarketRegime Classify(
        decimal trendPercent,
        decimal atrPercent,
        decimal breakoutThresholdPercent,
        decimal highVolatilityAtrPercent,
        decimal parabolicThresholdPercent)
    {
        if (Math.Abs(trendPercent) >= parabolicThresholdPercent)
            return trendPercent > 0 ? MarketRegime.ParabolicUp : MarketRegime.ParabolicDown;

        if (atrPercent >= highVolatilityAtrPercent)
            return MarketRegime.HighVolatility;

        if (Math.Abs(trendPercent) <= breakoutThresholdPercent / 2m)
            return MarketRegime.Ranging;

        if (Math.Abs(trendPercent) >= breakoutThresholdPercent)
            return MarketRegime.Breakout;

        return trendPercent > 0 ? MarketRegime.TrendingUp : MarketRegime.TrendingDown;
    }
}