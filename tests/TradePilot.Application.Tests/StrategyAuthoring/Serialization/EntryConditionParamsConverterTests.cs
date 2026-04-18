using System.Text.Json;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;

namespace TradePilot.Application.Tests.StrategyAuthoring.Serialization;

[TestClass]
public sealed class EntryConditionParamsConverterTests
{
    [TestMethod]
    public void GivenMacdCondition_WhenRoundTripped_ThenMacdParamsPreserved()
    {
        var condition = new EntryConditionConfig
        {
            Id = "cond-macd",
            Enabled = true,
            Type = EntryConditionType.Macd,
            Label = "MACD Cross",
            Params = new MacdParams { FastPeriod = 12, SlowPeriod = 26, SignalPeriod = 9, Operator = "gt" },
        };

        var json = JsonSerializer.Serialize(condition, StrategyJsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<EntryConditionConfig>(json, StrategyJsonOptions.Default);

        deserialized.Should().NotBeNull();
        deserialized!.Params.Should().BeOfType<MacdParams>();
        var macd = (MacdParams)deserialized.Params!;
        macd.FastPeriod.Should().Be(12);
        macd.SlowPeriod.Should().Be(26);
        macd.SignalPeriod.Should().Be(9);
        macd.Operator.Should().Be("gt");
    }

    [TestMethod]
    public void GivenPriceVsEmaCondition_WhenRoundTripped_ThenParamsPreserved()
    {
        var condition = new EntryConditionConfig
        {
            Id = "cond-ema",
            Enabled = true,
            Type = EntryConditionType.PriceVsEma,
            Label = "Price > EMA50",
            Params = new PriceVsEmaParams { Period = 50, Operator = "gt" },
        };

        var json = JsonSerializer.Serialize(condition, StrategyJsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<EntryConditionConfig>(json, StrategyJsonOptions.Default);

        deserialized.Should().NotBeNull();
        deserialized!.Params.Should().BeOfType<PriceVsEmaParams>();
        var ema = (PriceVsEmaParams)deserialized.Params!;
        ema.Period.Should().Be(50);
        ema.Operator.Should().Be("gt");
    }

    [TestMethod]
    public void GivenCandlePatternCondition_WhenRoundTripped_ThenParamsPreserved()
    {
        var condition = new EntryConditionConfig
        {
            Id = "cond-candle-pattern",
            Enabled = true,
            Type = EntryConditionType.CandlePattern,
            Label = "Bullish engulfing",
            Params = new CandlePatternParams { Pattern = "bullish_engulfing" },
        };

        var json = JsonSerializer.Serialize(condition, StrategyJsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<EntryConditionConfig>(json, StrategyJsonOptions.Default);

        deserialized.Should().NotBeNull();
        deserialized!.Type.Should().Be(EntryConditionType.CandlePattern);
        deserialized.Params.Should().BeOfType<CandlePatternParams>();
        ((CandlePatternParams)deserialized.Params!).Pattern.Should().Be("bullish_engulfing");
    }

    [TestMethod]
    public void GivenConditionWithNullParams_WhenDeserialized_ThenParamsIsNull()
    {
        const string json = """{"id":"c1","enabled":true,"type":"rsi","label":"test","params":null}""";

        var result = JsonSerializer.Deserialize<EntryConditionConfig>(json, StrategyJsonOptions.Default);

        result.Should().NotBeNull();
        result!.Params.Should().BeNull();
    }

    [TestMethod]
    public void GivenUnknownConditionType_WhenDeserialized_ThenUnknownParamsPreserved()
    {
        const string json = """{"id":"c2","enabled":true,"type":"custom_condition","label":"Custom","params":{"threshold":5,"mode":"fast"}}""";

        var result = JsonSerializer.Deserialize<EntryConditionConfig>(json, StrategyJsonOptions.Default);

        result.Should().NotBeNull();
        result!.Type.Should().Be(EntryConditionType.Unknown);
        result.Params.Should().BeOfType<UnknownConditionParams>();
        var unknown = (UnknownConditionParams)result.Params!;
        unknown.RawProperties.Should().ContainKey("threshold");
        unknown.RawProperties.Should().ContainKey("mode");
    }
}