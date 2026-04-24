using System.Globalization;
using System.Text.Json;
using TradePilot.Infrastructure.Hyperliquid;

namespace TradePilot.Infrastructure.Tests.Services;

[TestClass]
public sealed class HyperliquidFormattingTests
{
    [TestMethod]
    [DataRow("1.23000", "1.23")]
    [DataRow("100", "100")]
    [DataRow("0.00012340", "0.0001234")]
    [DataRow("1.0", "1")]
    public void GivenDecimalValue_WhenToWireDecimal_ThenTrailingZerosAreRemoved(string input, string expected)
    {
        var value = decimal.Parse(input, CultureInfo.InvariantCulture);

        var result = HyperliquidFormatting.ToWireDecimal(value);

        result.Should().Be(expected);
    }

    [TestMethod]
    public void GivenZero_WhenToWireDecimal_ThenReturnsZeroString()
    {
        var result = HyperliquidFormatting.ToWireDecimal(0m);

        result.Should().Be("0");
    }

    [TestMethod]
    [DataRow("B", "Buy")]
    [DataRow("A", "Sell")]
    [DataRow("b", "Buy")]
    [DataRow("a", "Sell")]
    [DataRow("Unknown", "Unknown")]
    public void GivenSideCode_WhenMapOrderSide_ThenReturnsExpectedDisplayValue(string input, string expected)
    {
        var result = HyperliquidFormatting.MapOrderSide(input);

        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("1.23", 1.23)]
    [DataRow("0", 0.0)]
    [DataRow("-5.5", -5.5)]
    public void GivenValidString_WhenParseDecimal_ThenReturnsParsedValue(string input, double expected)
    {
        var result = HyperliquidFormatting.ParseDecimal(input);

        result.Should().Be((decimal)expected);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not-a-number")]
    public void GivenInvalidString_WhenParseDecimal_ThenReturnsZero(string? input)
    {
        var result = HyperliquidFormatting.ParseDecimal(input);

        result.Should().Be(0m);
    }

    [TestMethod]
    public void GivenJsonNumber_WhenParseDecimal_ThenReturnsParsedValue()
    {
        using var document = JsonDocument.Parse("1.23");

        var result = HyperliquidFormatting.ParseDecimal(document.RootElement);

        result.Should().Be(1.23m);
    }

    [TestMethod]
    public void GivenJsonString_WhenParseDecimal_ThenReturnsParsedValue()
    {
        using var document = JsonDocument.Parse("\"1.23\"");

        var result = HyperliquidFormatting.ParseDecimal(document.RootElement);

        result.Should().Be(1.23m);
    }

    [TestMethod]
    public void GivenJsonNull_WhenParseDecimal_ThenReturnsZero()
    {
        using var document = JsonDocument.Parse("null");

        var result = HyperliquidFormatting.ParseDecimal(document.RootElement);

        result.Should().Be(0m);
    }
}