using TradePilot.Infrastructure.Binance;
using TradePilot.Infrastructure.Binance.Models;

namespace TradePilot.Infrastructure.Tests.Binance;

[TestClass]
public sealed class BinanceParsingTests
{
    [TestMethod]
    public void GivenValidDecimalString_WhenParseDecimal_ThenReturnsValue()
    {
        var result = BinanceParsing.ParseDecimal("123.456");

        result.Should().Be(123.456m);
    }

    [TestMethod]
    public void GivenScientificNotation_WhenParseDecimal_ThenReturnsValue()
    {
        var result = BinanceParsing.ParseDecimal("1.5e-4");

        result.Should().Be(0.00015m);
    }

    [TestMethod]
    public void GivenNullDecimalString_WhenParseDecimal_ThenThrowsFormatException()
    {
        var act = () => BinanceParsing.ParseDecimal(null);

        act.Should().Throw<FormatException>();
    }

    [TestMethod]
    public void GivenInvalidDecimalString_WhenParseDecimal_ThenThrowsFormatException()
    {
        var act = () => BinanceParsing.ParseDecimal("not-a-number");

        act.Should().Throw<FormatException>();
    }

    [TestMethod]
    public void GivenValidDecimalString_WhenTryParseDecimal_ThenReturnsTrueAndParsedValue()
    {
        var result = BinanceParsing.TryParseDecimal("42.5", out var parsed);

        result.Should().BeTrue();
        parsed.Should().Be(42.5m);
    }

    [TestMethod]
    public void GivenInvalidDecimalString_WhenTryParseDecimal_ThenReturnsFalseAndZero()
    {
        var result = BinanceParsing.TryParseDecimal("bad-value", out var parsed);

        result.Should().BeFalse();
        parsed.Should().Be(0m);
    }

    [TestMethod]
    public void GivenValidIntegerString_WhenParseInt_ThenReturnsValue()
    {
        var result = BinanceParsing.ParseInt("125");

        result.Should().Be(125);
    }

    [TestMethod]
    public void GivenInvalidIntegerString_WhenParseInt_ThenThrowsFormatException()
    {
        var act = () => BinanceParsing.ParseInt("12.5");

        act.Should().Throw<FormatException>();
    }

    [TestMethod]
    public void GivenValidOrderId_WhenParseOrderId_ThenReturnsLong()
    {
        var result = BinanceParsing.ParseOrderId("12345678");

        result.Should().Be(12345678L);
    }

    [TestMethod]
    public void GivenInvalidOrderId_WhenParseOrderId_ThenThrowsFormatException()
    {
        var act = () => BinanceParsing.ParseOrderId("abc");

        act.Should().Throw<FormatException>();
    }

    [TestMethod]
    public void GivenScientificNotationFundingRate_WhenToDto_ThenParsesRateAndMarkPrice()
    {
        var fundingRate = new BinanceFundingRate
        {
            Symbol = "BTCUSDT",
            FundingTime = 1700000000000L,
            FundingRateValue = "2.5e-5",
            MarkPriceValue = "5.0e4",
        };

        var dto = fundingRate.ToDto();

        dto.Rate.Should().Be(0.000025m);
        dto.MarkPrice.Should().Be(50000m);
    }
}