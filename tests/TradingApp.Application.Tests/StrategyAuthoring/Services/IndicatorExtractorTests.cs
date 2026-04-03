using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Services;

namespace TradingApp.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class IndicatorExtractorTests
{
    [TestMethod]
    public void GivenConfigWithRsiCondition_WhenExtract_ThenReturnsRsiRequirement()
    {
        var config = CreateConfigWithRsi(14);

        var result = IndicatorExtractor.Extract(config);

        result.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Type = "RSI", Period = 14 });
    }

    [TestMethod]
    public void GivenConfigWithNoConditions_WhenExtract_ThenReturnsEmpty()
    {
        var config = CreateConfig();

        var result = IndicatorExtractor.Extract(config);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void GivenConfigWithDisabledCondition_WhenExtract_ThenReturnsEmpty()
    {
        var config = CreateConfigWithRsi(14, enabled: false);

        var result = IndicatorExtractor.Extract(config);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void GivenConfigWithDuplicateRsiPeriods_WhenExtract_ThenDeduplicates()
    {
        var config = CreateConfig(
            CreateRsiCondition(14),
            CreateRsiCondition(14, operatorValue: "gt", value: 70m));

        var result = IndicatorExtractor.Extract(config);

        result.Should().ContainSingle();
    }

    private static StrategyConfig CreateConfig(params EntryConditionConfig[] conditions)
    {
        return new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            StrategyName = "Test",
            Market = "BTC-USD",
            EntryLogic = EntryLogic.All,
            EntryConditions = conditions.Length > 0 ? conditions.ToList() : null,
            Risk = new RiskConfig { PositionSizeValue = 100m }
        };
    }

    private static StrategyConfig CreateConfigWithRsi(int period, bool enabled = true)
    {
        return CreateConfig(CreateRsiCondition(period, enabled: enabled));
    }

    private static EntryConditionConfig CreateRsiCondition(
        int period = 14,
        string operatorValue = "lt",
        decimal value = 40m,
        bool enabled = true)
    {
        return new EntryConditionConfig
        {
            Id = Guid.NewGuid().ToString(),
            Enabled = enabled,
            Type = EntryConditionType.Rsi,
            Label = $"RSI({period})",
            Params = new RsiParams
            {
                Period = period,
                Operator = operatorValue,
                Value = value
            }
        };
    }
}