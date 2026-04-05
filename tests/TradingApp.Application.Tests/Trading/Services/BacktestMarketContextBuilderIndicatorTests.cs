using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Trading.Services;

[TestClass]
public sealed class BacktestMarketContextBuilderIndicatorTests
{
    [TestMethod]
    public void GivenIndicatorRequirements_WhenBuild_ThenIndicatorContextIsPopulated()
    {
        var sut = new BacktestMarketContextBuilder();
        var candles = CreateCandles(20);

        foreach (var candle in candles)
        {
            sut.UpdateIndicators(candle);
        }

        var result = sut.Build(
            candles[^1],
            null,
            null,
            [new IndicatorRequirement { Type = "RSI", Period = 14 }]);

        result.IndicatorContext.Should().NotBeNull();
        result.IndicatorContext!.GetRsi(14).Should().NotBeNull();
        result.IndicatorContext.GetPreviousRsi(14).Should().NotBeNull();
    }

    [TestMethod]
    public void GivenNoIndicatorRequirements_WhenBuild_ThenIndicatorContextIsNull()
    {
        var sut = new BacktestMarketContextBuilder();
        var candles = CreateCandles(5);

        foreach (var candle in candles)
        {
            sut.UpdateIndicators(candle);
        }

        var result = sut.Build(candles[^1], null, null);

        result.IndicatorContext.Should().BeNull();
    }

    [TestMethod]
    public void GivenMacdRequirement_WhenBuild_ThenMacdValuesArePopulated()
    {
        var sut = new BacktestMarketContextBuilder();
        var candles = CreateCandles(40);

        foreach (var candle in candles)
        {
            sut.UpdateIndicators(candle);
        }

        var result = sut.Build(
            candles[^1],
            null,
            null,
            [new IndicatorRequirement
            {
                Type = "MACD",
                FastPeriod = 12,
                SlowPeriod = 26,
                SignalPeriod = 9
            }]);

        result.IndicatorContext.Should().NotBeNull();
        result.IndicatorContext!.GetMacd(12, 26, 9).Should().NotBeNull();
        result.IndicatorContext.GetMacdSignal(12, 26, 9).Should().NotBeNull();
        result.IndicatorContext.GetMacdHistogram(12, 26, 9).Should().NotBeNull();
        result.IndicatorContext.GetPreviousMacd(12, 26, 9).Should().NotBeNull();
        result.IndicatorContext.GetPreviousMacdSignal(12, 26, 9).Should().NotBeNull();
        result.IndicatorContext.GetPreviousMacdHistogram(12, 26, 9).Should().NotBeNull();

        var line = result.IndicatorContext.GetMacd(12, 26, 9)!.Value;
        var signal = result.IndicatorContext.GetMacdSignal(12, 26, 9)!.Value;
        var histogram = result.IndicatorContext.GetMacdHistogram(12, 26, 9)!.Value;

        histogram.Should().Be(line - signal);
    }

    [TestMethod]
    public void GivenSmaRequirement_WhenBuild_ThenSmaValuesArePopulated()
    {
        var sut = new BacktestMarketContextBuilder();
        var candles = CreateCandles(10);

        foreach (var candle in candles)
        {
            sut.UpdateIndicators(candle);
        }

        var result = sut.Build(
            candles[^1],
            null,
            null,
            [new IndicatorRequirement { Type = "SMA", Period = 5 }]);

        result.IndicatorContext.Should().NotBeNull();
        result.IndicatorContext!.GetSma(5).Should().Be(107m);
        result.IndicatorContext.GetPreviousSma(5).Should().Be(106m);
    }

    [TestMethod]
    public void GivenSufficientCandles_WhenBuild_ThenLlmContextIsPopulated()
    {
        var sut = new BacktestMarketContextBuilder();
        var candles = CreateCandles(100);

        foreach (var candle in candles)
        {
            sut.UpdateIndicators(candle);
        }

        var result = sut.Build(candles[^1], null, null);

        result.LlmContext.Should().NotBeNull();
        result.LlmContext!.DerivedRegime.Should().BeOneOf(
            MarketRegime.Aggressive,
            MarketRegime.Normal,
            MarketRegime.Defensive,
            MarketRegime.RiskOff);
        result.LlmContext.Summary.Should().StartWith("Synthetic:");
        result.LlmContext.GeneratedAtUtc.Should().Be(candles[^1].Timestamp);
    }

    private static List<Candle> CreateCandles(int count)
    {
        var candles = new List<Candle>(count);

        for (var index = 0; index < count; index++)
        {
            candles.Add(CreateCandle(index + 1, 100m + index));
        }

        return candles;
    }

    private static Candle CreateCandle(int sequence, decimal close)
    {
        var timestamp = sequence * 60_000L;
        return Candle.Create(
            "Binance",
            "BTC",
            "15m",
            timestamp,
            close,
            close + 1m,
            close - 1m,
            close,
            1_000m,
            10);
    }
}