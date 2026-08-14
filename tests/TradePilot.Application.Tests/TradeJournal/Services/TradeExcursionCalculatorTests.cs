using TradePilot.Application.TradeJournal.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.TradeJournal.Services;

[TestClass]
public sealed class TradeExcursionCalculatorTests
{
    [TestMethod]
    public void GivenLongTradeAndInclusiveCandles_WhenCalculated_ThenFavourableAndAdverseExcursionsUseHighAndLow()
    {
        var candles = new[]
        {
            CreateCandle(1_000, 100m, 112m, 95m, 108m),
            CreateCandle(2_000, 108m, 125m, 90m, 120m),
        };

        var result = TradeExcursionCalculator.Calculate(TradeSide.Long, 100m, 2m, 120m, candles);

        result.MfeAmount.Should().Be(50m);
        result.MfePercent.Should().Be(25m);
        result.MaeAmount.Should().Be(-20m);
        result.MaePercent.Should().Be(-10m);
    }

    [TestMethod]
    public void GivenShortTradeWithNoFavourableMovement_WhenCalculated_ThenMfeIsZeroAndMaeIsNegative()
    {
        var candles = new[] { CreateCandle(1_000, 100m, 115m, 101m, 110m) };

        var result = TradeExcursionCalculator.Calculate(TradeSide.Short, 100m, 3m, 110m, candles);

        result.MfeAmount.Should().Be(0m);
        result.MfePercent.Should().Be(0m);
        result.MaeAmount.Should().Be(-45m);
        result.MaePercent.Should().Be(-15m);
    }

    private static Candle CreateCandle(long timestamp, decimal open, decimal high, decimal low, decimal close)
    {
        return Candle.Create("Hyperliquid", "BTC", "15m", timestamp, open, high, low, close, 1m, 1);
    }
}
