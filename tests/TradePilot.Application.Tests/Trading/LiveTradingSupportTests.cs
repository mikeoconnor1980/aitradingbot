using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading;
using TradePilot.Domain.Enums;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Tests.Trading;

[TestClass]
public sealed class LiveTradingSupportTests
{
    [TestMethod]
    public void GivenGridStrategy_WhenValidateLiveTrading_ThenReturnsSupported()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Grid,
            StrategyName = "Grid",
            Market = "BTC-USD",
            Timeframe = "15m",
            Direction = Direction.Long,
            Grid = new GridConfig { Levels = 5, Spacing = 0.5m, BreakdownThreshold = 2m },
            Exit = new ExitConfig(),
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.FixedNotional,
                PositionSizeValue = 100m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };

        var isSupported = LiveTradingSupport.TryValidate(config, out var reason);

        isSupported.Should().BeTrue();
        reason.Should().BeNull();
    }

    [TestMethod]
    public void GivenSpotDcaStrategy_WhenValidateLiveTrading_ThenReturnsSupported()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Dca,
            StrategyName = "DCA",
            Exchange = "Hyperliquid",
            AssetType = AssetType.Spot,
            Market = "BTC-USD",
            Timeframe = "1h",
            Direction = Direction.Long,
            Dca = new DcaConfig
            {
                Interval = DcaInterval.Hourly,
                TimeOfDayUtc = "00:00",
                BaseAmountUsd = 100m,
                Allocations =
                [
                    new DcaAllocation
                    {
                        Market = "BTC-USD",
                        WeightPercent = 100m,
                    }
                ],
            },
            Exit = new ExitConfig(),
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.FixedNotional,
                PositionSizeValue = 100m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };

        var isSupported = LiveTradingSupport.TryValidate(config, out var reason);

        isSupported.Should().BeTrue();
        reason.Should().BeNull();
    }

    [TestMethod]
    public void GivenPerpDcaStrategy_WhenValidateLiveTrading_ThenReturnsUnsupported()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Dca,
            StrategyName = "DCA",
            Exchange = "Hyperliquid",
            AssetType = AssetType.Perp,
            Market = "BTC-USD",
            Timeframe = "1h",
            Direction = Direction.Long,
            Dca = new DcaConfig
            {
                Interval = DcaInterval.Hourly,
                TimeOfDayUtc = "00:00",
                BaseAmountUsd = 100m,
                Allocations =
                [
                    new DcaAllocation
                    {
                        Market = "BTC-USD",
                        WeightPercent = 100m,
                    }
                ],
            },
            Exit = new ExitConfig(),
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.FixedNotional,
                PositionSizeValue = 100m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };

        var isSupported = LiveTradingSupport.TryValidate(config, out var reason);

        isSupported.Should().BeFalse();
        reason.Should().Be("Live DCA requires a spot asset type.");
    }
}