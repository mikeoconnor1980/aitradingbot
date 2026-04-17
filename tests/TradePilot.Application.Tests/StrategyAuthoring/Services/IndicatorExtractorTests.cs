using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;

namespace TradePilot.Application.Tests.StrategyAuthoring.Services;

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

    [TestMethod]
    public void GivenConfigWithEmaCrossTrendFilter_WhenExtract_ThenReturnsTwoEmaRequirements()
    {
        var config = CreateConfig();
        config = config with
        {
            TrendFilter = new TrendFilterConfig
            {
                Enabled = true,
                Type = TrendFilterType.EmaCross,
                FastPeriod = 50,
                SlowPeriod = 200,
            },
        };

        var result = IndicatorExtractor.Extract(config);

        result.Should().HaveCount(2);
        result.Should().Contain(requirement => requirement.Type == "EMA" && requirement.Period == 50);
        result.Should().Contain(requirement => requirement.Type == "EMA" && requirement.Period == 200);
    }

    [TestMethod]
    public void GivenConfigWithSmaCrossTrendFilter_WhenExtract_ThenReturnsTwoSmaRequirements()
    {
        var config = CreateConfig();
        config = config with
        {
            TrendFilter = new TrendFilterConfig
            {
                Enabled = true,
                Type = TrendFilterType.SmaCross,
                FastPeriod = 20,
                SlowPeriod = 50,
            },
        };

        var result = IndicatorExtractor.Extract(config);

        result.Should().HaveCount(2);
        result.Should().Contain(requirement => requirement.Type == "SMA" && requirement.Period == 20);
        result.Should().Contain(requirement => requirement.Type == "SMA" && requirement.Period == 50);
    }

    [TestMethod]
    public void GivenConfigWithPriceAboveEmaTrendFilter_WhenExtract_ThenReturnsOneEmaRequirement()
    {
        var config = CreateConfig();
        config = config with
        {
            TrendFilter = new TrendFilterConfig
            {
                Enabled = true,
                Type = TrendFilterType.PriceAboveEma,
                Period = 200,
            },
        };

        var result = IndicatorExtractor.Extract(config);

        result.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Type = "EMA", Period = 200 });
    }

    [TestMethod]
    public void GivenDisabledTrendFilter_WhenExtract_ThenReturnsEmpty()
    {
        var config = CreateConfig();
        config = config with
        {
            TrendFilter = new TrendFilterConfig
            {
                Enabled = false,
                Type = TrendFilterType.EmaCross,
                FastPeriod = 50,
                SlowPeriod = 200,
            },
        };

        var result = IndicatorExtractor.Extract(config);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void GivenTrendFilterAndConditionShareEmaPeriod_WhenExtract_ThenDeduplicates()
    {
        var config = CreateConfig(
            new EntryConditionConfig
            {
                Id = Guid.NewGuid().ToString(),
                Enabled = true,
                Type = EntryConditionType.PriceVsEma,
                Label = "Price near EMA",
                Params = new PriceVsEmaParams
                {
                    Period = 50,
                    Operator = "near",
                },
            });

        config = config with
        {
            TrendFilter = new TrendFilterConfig
            {
                Enabled = true,
                Type = TrendFilterType.PriceAboveEma,
                Period = 50,
            },
        };

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