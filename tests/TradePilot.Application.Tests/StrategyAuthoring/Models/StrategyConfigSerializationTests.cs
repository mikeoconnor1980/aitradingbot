using System.Text.Json;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;

namespace TradePilot.Application.Tests.StrategyAuthoring.Models;

[TestClass]
public sealed class StrategyConfigSerializationTests
{
    [TestMethod]
    public void GivenGridModeConfig_WhenSerializedAndDeserialized_ThenRoundTripsCorrectly()
    {
        var config = new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Grid,
            StrategyName = "BTC Grid Long",
            Exchange = "Hyperliquid",
            Market = "BTC-USD",
            Timeframe = "15m",
            Direction = Direction.Long,
            Enabled = true,
            Grid = new GridConfig
            {
                Levels = 10,
                Spacing = 0.5m,
                EntryMode = "auto_from_signal_candle",
                BreakdownThreshold = 1.5m,
            },
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
                StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 6m },
            },
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.PercentWallet,
                PositionSizeValue = 5m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };

        var json = JsonSerializer.Serialize(config, StrategyJsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<StrategyConfig>(json, StrategyJsonOptions.Default);

        deserialized.Should().NotBeNull();
        deserialized!.StrategyMode.Should().Be(StrategyMode.Grid);
        deserialized.Grid.Should().NotBeNull();
        deserialized.Grid!.Levels.Should().Be(10);
        deserialized.TrendFilter.Should().BeNull();
        deserialized.EntryConditions.Should().BeNull();
    }

    [TestMethod]
    public void GivenGridModeConfig_WhenSerialized_ThenEnumsAreSnakeCase()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Grid,
            Direction = Direction.Long,
            Risk = new RiskConfig { PositionSizeType = PositionSizeType.PercentWallet },
        };

        var json = JsonSerializer.Serialize(config, StrategyJsonOptions.Default);

        json.Should().Contain("\"strategyMode\":\"grid\"");
        json.Should().Contain("\"direction\":\"long\"");
        json.Should().Contain("\"positionSizeType\":\"percent_wallet\"");
    }

    [TestMethod]
    public void GivenRiskBasedConfig_WhenSerialized_ThenEnumIsSnakeCase()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1.5m,
                AutoLeverage = true,
            },
        };

        var json = JsonSerializer.Serialize(config, StrategyJsonOptions.Default);

        json.Should().Contain("\"positionSizeType\":\"risk_based\"");
        json.Should().Contain("\"riskPerTradePercent\":1.5");
        json.Should().Contain("\"autoLeverage\":true");
    }

    [TestMethod]
    public void GivenRiskBasedConfig_WhenSerializedAndDeserialized_ThenRoundTripsCorrectly()
    {
        var config = new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Signal,
            StrategyName = "Risk Test",
            Exchange = "Hyperliquid",
            Market = "BTC-USD",
            Timeframe = "15m",
            Direction = Direction.Long,
            Enabled = true,
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 3m },
                StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
            },
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1.0m,
                AutoLeverage = true,
                Leverage = 5m,
                MaxOpenTrades = 1,
            },
        };

        var json = JsonSerializer.Serialize(config, StrategyJsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<StrategyConfig>(json, StrategyJsonOptions.Default);

        deserialized.Should().NotBeNull();
        deserialized!.Risk.PositionSizeType.Should().Be(PositionSizeType.RiskBased);
        deserialized.Risk.RiskPerTradePercent.Should().Be(1.0m);
        deserialized.Risk.AutoLeverage.Should().BeTrue();
    }

    [TestMethod]
    public void GivenConfigWithNullOptionalSections_WhenSerialized_ThenNullsPresent()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Grid,
            TrendFilter = null,
            EntryConditions = null,
            Metadata = null,
            Source = null,
        };

        var json = JsonSerializer.Serialize(config, StrategyJsonOptions.Default);

        json.Should().Contain("\"trendFilter\":null");
        json.Should().Contain("\"entryConditions\":null");
        json.Should().Contain("\"metadata\":null");
        json.Should().Contain("\"source\":null");
    }

    [TestMethod]
    public void GivenConfigWithMetadataTags_WhenSerializedAndDeserialized_ThenTagsRoundTrip()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            StrategyName = "Tagged Strategy",
            Metadata = new StrategyMetadata
            {
                Tags = ["trend", "ema"],
                Notes = "Reusable strategy tags",
            },
        };

        var json = JsonSerializer.Serialize(config, StrategyJsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<StrategyConfig>(json, StrategyJsonOptions.Default);

        deserialized.Should().NotBeNull();
        deserialized!.Metadata.Should().NotBeNull();
        deserialized.Metadata!.Tags.Should().Equal("trend", "ema");
        deserialized.Metadata.Notes.Should().Be("Reusable strategy tags");
    }

    [TestMethod]
    public void GivenSignalModeWithRsiCondition_WhenRoundTripped_ThenRsiParamsPreserved()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            EntryLogic = TradePilot.Application.StrategyAuthoring.Models.EntryLogic.All,
            EntryConditions =
            [
                new EntryConditionConfig
                {
                    Id = "cond-1",
                    Enabled = true,
                    Type = EntryConditionType.Rsi,
                    Label = "RSI Pullback",
                    Params = new RsiParams { Period = 14, Operator = "lt", Value = 40 },
                },
            ],
        };

        var json = JsonSerializer.Serialize(config, StrategyJsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<StrategyConfig>(json, StrategyJsonOptions.Default);

        deserialized.Should().NotBeNull();
        deserialized!.EntryConditions.Should().HaveCount(1);
        var condition = deserialized.EntryConditions![0];
        condition.Type.Should().Be(EntryConditionType.Rsi);
        condition.Params.Should().BeOfType<RsiParams>();
        var rsi = (RsiParams)condition.Params!;
        rsi.Period.Should().Be(14);
        rsi.Operator.Should().Be("lt");
        rsi.Value.Should().Be(40);
    }

    [TestMethod]
    public void GivenDcaModeConfig_WhenSerializedAndDeserialized_ThenRoundTripsCorrectly()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Dca,
            StrategyName = "Portfolio DCA",
            Exchange = "Hyperliquid",
            AssetType = AssetType.Spot,
            Market = "MULTI-ASSET",
            Timeframe = "1d",
            Direction = Direction.Long,
            Dca = new DcaConfig
            {
                Interval = DcaInterval.Monthly,
                DayOfMonth = 1,
                TimeOfDayUtc = "00:00",
                BaseAmountUsd = 250m,
                Allocations =
                [
                    new DcaAllocation { Market = "BTC-USD", WeightPercent = 60m },
                    new DcaAllocation { Market = "ETH-USD", WeightPercent = 40m },
                ],
                GateConditions = new DcaGateConfig
                {
                    MaxPriceUsd = 100_000m,
                    MaxFearGreedIndex = 45,
                },
                ScalingBands =
                [
                    new DcaScalingBand { PriceUpperUsd = 60_000m, ScalingPercent = 20m },
                ],
                ProfitTaking = new DcaProfitTakingConfig
                {
                    Tiers =
                    [
                        new DcaProfitTier { TargetMultiple = 2m, SellPercent = 25m },
                    ],
                },
            },
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.FixedNotional,
                PositionSizeValue = 250m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };

        var json = JsonSerializer.Serialize(config, StrategyJsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<StrategyConfig>(json, StrategyJsonOptions.Default);

        json.Should().Contain("\"strategyMode\":\"dca\"");
        json.Should().Contain("\"assetType\":\"spot\"");
        deserialized.Should().NotBeNull();
        deserialized!.StrategyMode.Should().Be(StrategyMode.Dca);
        deserialized.AssetType.Should().Be(AssetType.Spot);
        deserialized.Dca.Should().NotBeNull();
        deserialized.Dca!.Allocations.Should().HaveCount(2);
        deserialized.Dca.GateConditions!.MaxFearGreedIndex.Should().Be(45);
        deserialized.Dca.ProfitTaking!.Tiers.Should().ContainSingle();
    }
}