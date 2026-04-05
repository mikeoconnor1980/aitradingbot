using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Tests.Trading.Services;

[TestClass]
public sealed class GridStrategyEngineRegimeTests
{
    private readonly GridStrategyEngine _sut = new();

    [TestMethod]
    public async Task GivenRiskOffRegime_WhenEvaluated_ThenSetupNotDetected()
    {
        var context = CreateMarketContextWithRegime(MarketRegime.RiskOff);
        var config = CreateGridConfig();

        var result = await _sut.EvaluateAsync(context, config);

        result.SetupDetected.Should().BeFalse();
        result.Regime.Should().Be(MarketRegime.RiskOff);
        result.Reason.Should().Contain("RiskOff");
    }

    [TestMethod]
    public async Task GivenAggressiveRegime_WhenEvaluated_ThenSetupDetected()
    {
        var context = CreateMarketContextWithRegime(MarketRegime.Aggressive);
        var config = CreateGridConfig();

        var result = await _sut.EvaluateAsync(context, config);

        result.SetupDetected.Should().BeTrue();
        result.Regime.Should().Be(MarketRegime.Aggressive);
    }

    [TestMethod]
    public async Task GivenDefensiveRegime_WhenEvaluated_ThenSetupDetected()
    {
        var context = CreateMarketContextWithRegime(MarketRegime.Defensive);
        var config = CreateGridConfig();

        var result = await _sut.EvaluateAsync(context, config);

        result.SetupDetected.Should().BeTrue();
        result.Regime.Should().Be(MarketRegime.Defensive);
    }

    [TestMethod]
    public async Task GivenNormalRegime_WhenEvaluated_ThenSetupDetected()
    {
        var context = CreateMarketContextWithRegime(MarketRegime.Normal);
        var config = CreateGridConfig();

        var result = await _sut.EvaluateAsync(context, config);

        result.SetupDetected.Should().BeTrue();
        result.Regime.Should().Be(MarketRegime.Normal);
    }

    [TestMethod]
    public async Task GivenNoLlmContext_WhenEvaluated_ThenDefaultsToNormalAndSetupDetected()
    {
        var context = CreateMarketContextWithRegime(null);
        var config = CreateGridConfig();

        var result = await _sut.EvaluateAsync(context, config);

        result.SetupDetected.Should().BeTrue();
        result.Regime.Should().Be(MarketRegime.Normal);
    }

    [TestMethod]
    public async Task GivenRiskOffButIncompleteGrid_WhenEvaluated_ThenRejectsForIncompleteConfig()
    {
        var context = CreateMarketContextWithRegime(MarketRegime.RiskOff);
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Grid,
            StrategyName = "Test",
            Market = "BTC-USD",
            Grid = new GridConfig { Levels = 0, Spacing = 0m },
            Risk = new RiskConfig { PositionSizeValue = 100m }
        };

        var result = await _sut.EvaluateAsync(context, config);

        result.SetupDetected.Should().BeFalse();
        result.Reason.Should().Be("Grid configuration is incomplete.");
    }

    private static MarketContext CreateMarketContextWithRegime(MarketRegime? regime)
    {
        LlmContext? llmContext = regime.HasValue
            ? new LlmContext
            {
                MarketSentiment = "Neutral",
                MacroRegime = "Neutral",
                EventRisk = "Low",
                Confidence = 0.75m,
                DerivedRegime = regime.Value,
                GeneratedAtUtc = 1_000_000
            }
            : null;

        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = 1_000_000,
            CurrentCandle = CreateCandle("15m", 1_000_000),
            LatestOneHourCandle = CreateCandle("1h", 1_000_000),
            LatestFourHourCandle = CreateCandle("4h", 1_000_000),
            Indicators = new IndicatorSnapshot(),
            LlmContext = llmContext
        };
    }

    private static StrategyConfig CreateGridConfig()
    {
        return new StrategyConfig
        {
            StrategyMode = StrategyMode.Grid,
            StrategyName = "Grid Strategy",
            Market = "BTC-USD",
            Grid = new GridConfig
            {
                Levels = 5,
                Spacing = 0.5m,
            },
            Risk = new RiskConfig
            {
                PositionSizeValue = 100m,
            }
        };
    }

    private static Candle CreateCandle(string interval, long timestamp)
    {
        return Candle.Create("Binance", "BTC-USD", interval, timestamp, 100m, 105m, 95m, 102m, 1_000m, 10);
    }
}
