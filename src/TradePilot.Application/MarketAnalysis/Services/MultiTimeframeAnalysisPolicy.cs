using TradePilot.Application.MarketAnalysis.Models;

namespace TradePilot.Application.MarketAnalysis.Services;

internal static class MultiTimeframeAnalysisPolicy
{
    /// <summary>
    /// Composes ordered Phase 2 results without recalculating any single-timeframe fact.
    /// </summary>
    public static MultiTimeframeMarketAnalysisResult Compose(
        string symbol,
        DateTimeOffset generatedAt,
        IReadOnlyList<TimeframeMarketAnalysis> timeframes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(timeframes);

        if (timeframes.Count < 2)
        {
            throw new ArgumentException("At least two timeframe analyses are required.", nameof(timeframes));
        }

        var shortTerm = timeframes[0];
        var primary = timeframes[^1];
        var bullishTrendCount = timeframes.Count(item => item.Analysis.Trend == MarketTrend.Bullish);
        var bearishTrendCount = timeframes.Count(item => item.Analysis.Trend == MarketTrend.Bearish);
        var neutralTrendCount = timeframes.Count(item => item.Analysis.Trend == MarketTrend.Neutral);
        var bullishMomentumCount = timeframes.Count(item => item.Analysis.Momentum == MarketMomentum.Bullish);
        var bearishMomentumCount = timeframes.Count(item => item.Analysis.Momentum == MarketMomentum.Bearish);
        var neutralMomentumCount = timeframes.Count(item => item.Analysis.Momentum == MarketMomentum.Neutral);
        var bullishStructureCount = timeframes.Count(
            item => item.Analysis.MarketStructure == MarketStructure.HigherHighHigherLow);
        var bearishStructureCount = timeframes.Count(
            item => item.Analysis.MarketStructure == MarketStructure.LowerHighLowerLow);
        var rangeStructureCount = timeframes.Count(item => item.Analysis.MarketStructure == MarketStructure.Range);
        var mixedStructureCount = timeframes.Count(item => item.Analysis.MarketStructure == MarketStructure.Mixed);
        var unknownStructureCount = timeframes.Count(item => item.Analysis.MarketStructure == MarketStructure.Unknown);
        var lowVolatilityCount = timeframes.Count(item => item.Analysis.VolatilityRegime == VolatilityRegime.Low);
        var normalVolatilityCount = timeframes.Count(item => item.Analysis.VolatilityRegime == VolatilityRegime.Normal);
        var highVolatilityCount = timeframes.Count(item => item.Analysis.VolatilityRegime == VolatilityRegime.High);
        var reference = primary.Analysis;

        var trendConflicts = timeframes
            .Take(timeframes.Count - 1)
            .Where(item => item.Analysis.Trend != reference.Trend)
            .Select(item => new TimeframeClassificationConflict<MarketTrend>(
                item.Timeframe,
                item.Analysis.Trend,
                primary.Timeframe,
                reference.Trend))
            .ToList();
        var momentumConflicts = timeframes
            .Take(timeframes.Count - 1)
            .Where(item => item.Analysis.Momentum != reference.Momentum)
            .Select(item => new TimeframeClassificationConflict<MarketMomentum>(
                item.Timeframe,
                item.Analysis.Momentum,
                primary.Timeframe,
                reference.Momentum))
            .ToList();
        var structureConflicts = timeframes
            .Take(timeframes.Count - 1)
            .Where(item => item.Analysis.MarketStructure != reference.MarketStructure)
            .Select(item => new TimeframeClassificationConflict<MarketStructure>(
                item.Timeframe,
                item.Analysis.MarketStructure,
                primary.Timeframe,
                reference.MarketStructure))
            .ToList();
        var volatilityConflicts = timeframes
            .Take(timeframes.Count - 1)
            .Where(item => item.Analysis.VolatilityRegime != reference.VolatilityRegime)
            .Select(item => new TimeframeClassificationConflict<VolatilityRegime>(
                item.Timeframe,
                item.Analysis.VolatilityRegime,
                primary.Timeframe,
                reference.VolatilityRegime))
            .ToList();

        return new MultiTimeframeMarketAnalysisResult(
            symbol,
            generatedAt,
            timeframes,
            primary.Timeframe,
            shortTerm.Timeframe,
            reference.Trend,
            shortTerm.Analysis.Trend,
            ClassifyDirection(bullishTrendCount, bearishTrendCount, neutralTrendCount),
            ClassifyDirection(bullishMomentumCount, bearishMomentumCount, neutralMomentumCount),
            ClassifyStructure(
                bullishStructureCount,
                bearishStructureCount,
                rangeStructureCount,
                mixedStructureCount,
                unknownStructureCount),
            ClassifyVolatility(lowVolatilityCount, normalVolatilityCount, highVolatilityCount),
            bullishTrendCount,
            bearishTrendCount,
            neutralTrendCount,
            bullishMomentumCount,
            bearishMomentumCount,
            neutralMomentumCount,
            bullishStructureCount,
            bearishStructureCount,
            rangeStructureCount,
            mixedStructureCount,
            unknownStructureCount,
            lowVolatilityCount,
            normalVolatilityCount,
            highVolatilityCount,
            new MultiTimeframeMarketAnalysisConflicts(
                shortTerm.Analysis.Trend != reference.Trend,
                bullishTrendCount > 0 && bearishTrendCount > 0,
                trendConflicts,
                momentumConflicts,
                structureConflicts,
                volatilityConflicts));
    }

    /// <summary>
    /// Classifies directional values using unanimity first, then a strict majority without an opposing direction.
    /// </summary>
    private static DirectionalAlignment ClassifyDirection(int bullish, int bearish, int neutral)
    {
        var total = bullish + bearish + neutral;

        if (bullish == total)
        {
            return DirectionalAlignment.AlignedBullish;
        }

        if (bearish == total)
        {
            return DirectionalAlignment.AlignedBearish;
        }

        if (neutral == total)
        {
            return DirectionalAlignment.AlignedNeutral;
        }

        if (bullish > total / 2 && bearish == 0)
        {
            return DirectionalAlignment.MostlyBullish;
        }

        if (bearish > total / 2 && bullish == 0)
        {
            return DirectionalAlignment.MostlyBearish;
        }

        return DirectionalAlignment.Mixed;
    }

    /// <summary>
    /// Classifies structure while preserving Range, Mixed, and Unknown as distinct Phase 2 values.
    /// </summary>
    private static StructureAlignment ClassifyStructure(
        int bullish,
        int bearish,
        int range,
        int mixed,
        int unknown)
    {
        var total = bullish + bearish + range + mixed + unknown;

        if (bullish == total)
        {
            return StructureAlignment.AlignedHigherHighHigherLow;
        }

        if (bearish == total)
        {
            return StructureAlignment.AlignedLowerHighLowerLow;
        }

        if (range == total)
        {
            return StructureAlignment.AlignedRange;
        }

        if (mixed == total)
        {
            return StructureAlignment.AlignedMixed;
        }

        if (unknown == total)
        {
            return StructureAlignment.AlignedUnknown;
        }

        if (bullish > total / 2 && bearish == 0)
        {
            return StructureAlignment.MostlyBullish;
        }

        if (bearish > total / 2 && bullish == 0)
        {
            return StructureAlignment.MostlyBearish;
        }

        return StructureAlignment.Mixed;
    }

    /// <summary>
    /// Classifies volatility as aligned only when every Phase 2 regime is equal.
    /// </summary>
    private static VolatilityAlignment ClassifyVolatility(int low, int normal, int high)
    {
        var total = low + normal + high;

        return (low, normal, high) switch
        {
            var counts when counts.low == total => VolatilityAlignment.AlignedLow,
            var counts when counts.normal == total => VolatilityAlignment.AlignedNormal,
            var counts when counts.high == total => VolatilityAlignment.AlignedHigh,
            _ => VolatilityAlignment.Mixed,
        };
    }
}
