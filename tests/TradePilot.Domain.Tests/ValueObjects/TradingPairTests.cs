using TradePilot.Domain.Enums;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Domain.Tests.ValueObjects;

[TestClass]
public sealed class TradingPairTests
{
    [TestMethod]
    public void GivenCanonicalTradingPair_WhenParsed_ThenRoundTrips()
    {
        var pair = TradingPair.Parse("BTC/USD:PERP");

        pair.Base.Should().Be("BTC");
        pair.Quote.Should().Be("USD");
        pair.ProductType.Should().Be(AssetType.Perp);
        pair.Canonical.Should().Be("BTC/USD:PERP");
    }

    [TestMethod]
    public void GivenUsdtQuote_WhenCreated_ThenNormalizesToUsd()
    {
        var pair = TradingPair.Create("eth", "usdt", AssetType.Spot);

        pair.Base.Should().Be("ETH");
        pair.Quote.Should().Be("USD");
        pair.Canonical.Should().Be("ETH/USD:SPOT");
    }

    [TestMethod]
    public void GivenEquivalentInputs_WhenCreated_ThenEqualityUsesNormalizedValues()
    {
        var first = TradingPair.Create("BTC", "USD", AssetType.Perp);
        var second = TradingPair.Parse("btc/usdt:perp");

        first.Should().Be(second);
    }

    [TestMethod]
    public void GivenInvalidCanonicalFormat_WhenParsed_ThenThrowsFormatException()
    {
        var act = () => TradingPair.Parse("BTC-PERP");

        act.Should().Throw<FormatException>();
    }

    [TestMethod]
    public void GivenUnsupportedQuote_WhenCreated_ThenThrowsArgumentOutOfRangeException()
    {
        var act = () => TradingPair.Create("BTC", "EUR", AssetType.Perp);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}