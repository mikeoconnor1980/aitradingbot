using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Tests.Trading.Services;

[TestClass]
public sealed class DcaControllerTests
{
    private readonly DcaController _sut = new();

    [TestMethod]
    public async Task GivenDueHourlyDcaWindow_WhenProcessAsync_ThenEmitsScaledDcaBuy()
    {
        var config = CreateConfig(new DcaConfig
        {
            Interval = DcaInterval.Hourly,
            TimeOfDayUtc = "00:00",
            BaseAmountUsd = 100m,
            Allocations =
            [
                new DcaAllocation
                {
                    Market = "BTC-PERP",
                    WeightPercent = 100m,
                }
            ],
            ScalingBands =
            [
                new DcaScalingBand
                {
                    PriceLowerUsd = 80m,
                    PriceUpperUsd = 100m,
                    ScalingPercent = 20m,
                }
            ],
        });
        var context = CreateContext("BTC", 90m, new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero));
        context.DrawdownScalingFactor = 0.50m;

        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = true },
            context,
            new GridState(),
            new PositionState(),
            config);

        signals.Should().ContainSingle();
        var signal = signals[0];
        var parameters = signal.Parameters!;

        signal.SignalType.Should().Be("OpenPosition");
        signal.Symbol.Should().Be("BTC-PERP");
        parameters["tradeType"].Should().Be(TradeType.DcaBuy.ToString());
        parameters["notionalUsd"].Should().Be(60m);
        parameters["size"].Should().Be(0.66666667m);
    }

    [TestMethod]
    public async Task GivenPerpContextSymbol_WhenProcessAsync_ThenEmitsConfiguredPerpMarket()
    {
        var config = CreateConfig(new DcaConfig
        {
            Interval = DcaInterval.Hourly,
            TimeOfDayUtc = "00:00",
            BaseAmountUsd = 100m,
            Allocations =
            [
                new DcaAllocation
                {
                    Market = "BTC-PERP",
                    WeightPercent = 100m,
                }
            ],
        }, AssetType.Perp, "BTC-PERP");

        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = true },
            CreateContext("BTC-PERP", 95m, new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero)),
            new GridState(),
            new PositionState(),
            config);

        signals.Should().ContainSingle();
        signals[0].Symbol.Should().Be("BTC-PERP");
    }

    [TestMethod]
    public async Task GivenScheduleNotDue_WhenProcessAsync_ThenReturnsNoSignals()
    {
        var config = CreateConfig(new DcaConfig
        {
            Interval = DcaInterval.Daily,
            TimeOfDayUtc = "12:00",
            BaseAmountUsd = 100m,
            Allocations =
            [
                new DcaAllocation
                {
                    Market = "BTC-PERP",
                    WeightPercent = 100m,
                }
            ],
        });

        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = true },
            CreateContext("BTC", 95m, new DateTimeOffset(2026, 1, 5, 11, 0, 0, TimeSpan.Zero)),
            new GridState(),
            new PositionState(),
            config);

        signals.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenDueFiveMinuteDcaWindow_WhenProcessAsync_ThenEmitsSignal()
    {
        var config = CreateConfig(new DcaConfig
        {
            Interval = DcaInterval.FiveMinutes,
            TimeOfDayUtc = "00:05",
            BaseAmountUsd = 100m,
            Allocations =
            [
                new DcaAllocation
                {
                    Market = "BTC-PERP",
                    WeightPercent = 100m,
                }
            ],
        });

        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = true },
            CreateContext("BTC", 95m, new DateTimeOffset(2026, 1, 5, 11, 5, 0, TimeSpan.Zero), "5m"),
            new GridState(),
            new PositionState(),
            config);

        signals.Should().ContainSingle();
        signals[0].Reason.Should().Contain("FiveMinutes");
    }

    [TestMethod]
    public async Task GivenFiveMinuteScheduleSameModuloDifferentMinute_WhenProcessAsync_ThenEmitsSignal()
    {
        var config = CreateConfig(new DcaConfig
        {
            Interval = DcaInterval.FiveMinutes,
            TimeOfDayUtc = "00:05",
            BaseAmountUsd = 100m,
            Allocations =
            [
                new DcaAllocation
                {
                    Market = "BTC-PERP",
                    WeightPercent = 100m,
                }
            ],
        });

        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = true },
            CreateContext("BTC", 95m, new DateTimeOffset(2026, 1, 5, 11, 45, 0, TimeSpan.Zero), "5m"),
            new GridState(),
            new PositionState(),
            config);

        signals.Should().ContainSingle();
    }

    [TestMethod]
    public async Task GivenFiveMinuteScheduleOffBoundary_WhenProcessAsync_ThenReturnsNoSignals()
    {
        var config = CreateConfig(new DcaConfig
        {
            Interval = DcaInterval.FiveMinutes,
            TimeOfDayUtc = "00:05",
            BaseAmountUsd = 100m,
            Allocations =
            [
                new DcaAllocation
                {
                    Market = "BTC-USD",
                    WeightPercent = 100m,
                }
            ],
        });

        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = true },
            CreateContext("BTC", 95m, new DateTimeOffset(2026, 1, 5, 11, 7, 0, TimeSpan.Zero), "5m"),
            new GridState(),
            new PositionState(),
            config);

        signals.Should().BeEmpty();
    }

    private static StrategyConfig CreateConfig(DcaConfig dca, AssetType assetType = AssetType.Perp, string market = "BTC-PERP")
    {
        return new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Dca,
            StrategyName = "Test DCA",
            Exchange = "Hyperliquid",
            AssetType = assetType,
            Market = market,
            Timeframe = "1h",
            Direction = Direction.Long,
            Dca = dca,
            Exit = new ExitConfig(),
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.FixedNotional,
                PositionSizeValue = dca.BaseAmountUsd,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };
    }

    private static MarketContext CreateContext(string symbol, decimal close, DateTimeOffset timestamp, string interval = "1h")
    {
        var timestampUtc = timestamp.ToUnixTimeMilliseconds();

        return new MarketContext
        {
            Symbol = symbol,
            TimestampUtc = timestampUtc,
            CurrentCandle = Candle.Create(
                "Binance",
                symbol,
                interval,
                timestampUtc,
                close,
                close,
                close,
                close,
                1_000m,
                10),
            LatestOneHourCandle = null,
            LatestFourHourCandle = null,
            Indicators = new IndicatorSnapshot(),
            AccountEquity = 10_000m,
            DrawdownScalingFactor = 1m,
        };
    }
}