using TradePilot.Domain.Entities;

namespace TradePilot.Domain.Tests.Entities;

[TestClass]
public sealed class FundingRateTests
{
    [TestMethod]
    public void GivenValidParameters_WhenCreate_ThenPropertiesAreSet()
    {
        var fundingRate = FundingRate.Create("BTC", 1700000000000L, 0.0001m, 50000m);

        fundingRate.Symbol.Should().Be("BTC");
        fundingRate.Timestamp.Should().Be(1700000000000L);
        fundingRate.Rate.Should().Be(0.0001m);
        fundingRate.MarkPrice.Should().Be(50000m);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void GivenInvalidSymbol_WhenCreate_ThenThrowsArgumentException(string? symbol)
    {
        var act = () => FundingRate.Create(symbol!, 1700000000000L, 0.0001m, 50000m);

        act.Should().Throw<ArgumentException>();
    }
}