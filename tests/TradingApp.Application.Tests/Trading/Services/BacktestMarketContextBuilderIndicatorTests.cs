using TradingApp.Application.StrategyAuthoring.Models;
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