using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Signals.Implementations;
using TradePilot.Application.Trading.Signals.Registry;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class DerivedSignalConditionHandlerTests
{
    private DerivedSignalConditionHandler _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        var registry = new DerivedSignalRegistry();
        registry.Register(new CandlePatternSignal());
        registry.Register(new LiquiditySweepSignal());
        registry.Register(new StructureShiftSignal());
        _sut = new DerivedSignalConditionHandler(registry);
    }

    [TestMethod]
    public void GivenBullishEngulfingPattern_WhenHistoryMatches_ThenConditionPassesWithMetadata()
    {
        var condition = new EntryConditionConfig
        {
            Id = "pattern-1",
            Enabled = true,
            Type = EntryConditionType.CandlePattern,
            Label = "Bullish engulfing",
            Params = new CandlePatternParams
            {
                Pattern = "bullish_engulfing",
            },
        };
        var marketContext = CreateMarketContext(
            [
                CreateCandle(1, 105m, 106m, 99m, 100m),
                CreateCandle(2, 99m, 107m, 98m, 106m),
            ]);

        var result = _sut.Evaluate(condition, new IndicatorContext(), marketContext);

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(1m);
        var metadata = result.Metadata ?? throw new AssertFailedException("Expected metadata for a matched candle pattern signal.");
        metadata.Should().ContainKey("pattern");
        metadata["pattern"].Should().Be("bullish_engulfing");
    }

    [TestMethod]
    public void GivenMissingCandleHistory_WhenEvaluated_ThenConditionFailsClearly()
    {
        var condition = new EntryConditionConfig
        {
            Id = "pattern-1",
            Enabled = true,
            Type = EntryConditionType.CandlePattern,
            Label = "Bullish engulfing",
            Params = new CandlePatternParams
            {
                Pattern = "bullish_engulfing",
            },
        };
        var currentCandle = CreateCandle(2, 99m, 107m, 98m, 106m);
        var marketContext = new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = currentCandle.Timestamp,
            CurrentCandle = currentCandle,
            PreviousCandle = null,
            Indicators = new IndicatorSnapshot(),
            IndicatorContext = new IndicatorContext(),
        };

        var result = _sut.Evaluate(condition, new IndicatorContext(), marketContext);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("candle history");
    }

    private static MarketContext CreateMarketContext(IReadOnlyList<Candle> candles)
    {
        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = candles[^1].Timestamp,
            CurrentCandle = candles[^1],
            PreviousCandle = candles.Count > 1 ? candles[^2] : null,
            Indicators = new IndicatorSnapshot(),
            IndicatorContext = new IndicatorContext(),
            CandleHistory = candles,
        };
    }

    private static Candle CreateCandle(long timestamp, decimal open, decimal high, decimal low, decimal close)
    {
        return Candle.Create(
            "Test",
            "BTC-USD",
            "15m",
            timestamp,
            open,
            high,
            low,
            close,
            10m,
            1);
    }
}