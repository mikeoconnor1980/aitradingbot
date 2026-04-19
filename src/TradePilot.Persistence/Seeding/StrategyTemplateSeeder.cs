using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Domain.Entities;
using EntryLogicEnum = TradePilot.Application.StrategyAuthoring.Models.EntryLogic;

namespace TradePilot.Persistence.Seeding;

public static class StrategyTemplateSeeder
{
    public static async Task SeedAsync(TradePilotDbContext db, CancellationToken cancellationToken = default)
    {
        var templates = BuildTemplates();
        var existingSlugs = await db.StrategyTemplates
            .Where(t => templates.Select(d => d.Slug).Contains(t.Slug))
            .ToDictionaryAsync(t => t.Slug, cancellationToken);

        foreach (var definition in templates)
        {
            var configJson = JsonSerializer.Serialize(definition.Config, StrategyJsonOptions.Default);
            var tagsJson = JsonSerializer.Serialize(definition.Tags);

            if (existingSlugs.TryGetValue(definition.Slug, out var existing))
            {
                existing.Update(
                    definition.Name,
                    definition.Description,
                    definition.Config.StrategyMode.ToString().ToLowerInvariant(),
                    definition.Config.Direction.ToString().ToLowerInvariant(),
                    definition.Config.Market,
                    tagsJson,
                    configJson,
                    definition.SortOrder,
                    isSystemTemplate: true);
                existing.SetBeginnerVisibility(definition.IsBeginnerVisible);
            }
            else
            {
                var template = StrategyTemplate.Create(
                    definition.Slug,
                    definition.Name,
                    definition.Description,
                    definition.Config.StrategyMode.ToString().ToLowerInvariant(),
                    definition.Config.Direction.ToString().ToLowerInvariant(),
                    definition.Config.Market,
                    tagsJson,
                    configJson,
                    definition.SortOrder,
                    isSystemTemplate: true);
                template.SetBeginnerVisibility(definition.IsBeginnerVisible);

                db.StrategyTemplates.Add(template);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static List<TemplateDefinition> BuildTemplates() =>
    [
        TrendPullbackEmaLong(),
        VwapIntradayPullbackLong(),
        RangeReversionRsiLong(),
        BtcDcaBelow90000(),
    ];

    private static TemplateDefinition TrendPullbackEmaLong() => new(
        Slug: "trend-pullback-ema-long",
        Name: "Trend Pullback EMA Long",
        Description: "Buy pullbacks in an established uptrend. Waits for price to pull back to the 20 EMA in a bullish trend confirmed by the 50 EMA, then enters on a bullish candle pattern.",
        Tags: ["starter", "trend", "ema"],
        SortOrder: 1,
        IsBeginnerVisible: true,
        Config: new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Signal,
            StrategyName = "Trend Pullback EMA Long",
            Exchange = "Hyperliquid",
            Market = "BTCUSDT",
            Timeframe = "5m",
            Direction = Direction.Long,
            Enabled = true,
            TemplateId = "trend-pullback-ema-long",
            TrendFilter = new TrendFilterConfig
            {
                Enabled = true,
                Type = TrendFilterType.PriceAboveEma,
                Period = 50,
                FastPeriod = 0,
                SlowPeriod = 0,
                Operator = TrendOperator.Above,
                AppliesTo = Direction.Long,
            },
            EntryLogic = EntryLogicEnum.All,
            EntryConditions =
            [
                new EntryConditionConfig
                {
                    Id = "pullback-to-ema20",
                    Enabled = true,
                    Type = EntryConditionType.PriceVsEma,
                    Label = "Price touches EMA 20",
                    Params = new PriceVsEmaParams
                    {
                        Period = 20,
                        Operator = "lte",
                        DistanceType = "percent",
                    },
                },
                new EntryConditionConfig
                {
                    Id = "rsi-above-50",
                    Enabled = true,
                    Type = EntryConditionType.Rsi,
                    Label = "RSI(14) > 50",
                    Params = new RsiParams
                    {
                        Period = 14,
                        Operator = "gt",
                        Value = 50m,
                    },
                },
                new EntryConditionConfig
                {
                    Id = "bullish-candle",
                    Enabled = true,
                    Type = EntryConditionType.CandlePattern,
                    Label = "Bullish rejection or engulfing",
                    Params = new CandlePatternParams
                    {
                        Pattern = "bullish_rejection_or_engulfing",
                    },
                },
            ],
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.RMultiple,
                    Value = 2.0m,
                },
                StopLoss = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.SwingLow,
                    Lookback = 10,
                },
                ExitOnOppositeSignal = false,
            },
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                PositionSizeValue = 0m,
                RiskPerTradePercent = 1.0m,
                Leverage = 1m,
                MaxOpenTrades = 1,
                CooldownValue = 3,
                CooldownUnit = CooldownUnit.Candles,
                AllowSameCandleReentry = false,
            },
            Metadata = new StrategyMetadata
            {
                Tags = ["starter", "trend", "ema"],
                Notes = string.Empty,
            },
            Source = new SourceMetadata
            {
                EntryPoint = StrategyEntryPoint.Migration,
                Summary = "Built-in starter template: Trend Pullback EMA Long",
            },
        });

    private static TemplateDefinition VwapIntradayPullbackLong() => new(
        Slug: "vwap-intraday-pullback-long",
        Name: "VWAP Intraday Pullback Long",
        Description: "Buy intraday pullbacks in an uptrend. Uses EMA 9 as a proxy for VWAP support and enters on bullish continuation patterns. Best suited for London and New York sessions.",
        Tags: ["daytrading", "vwap", "intraday"],
        SortOrder: 2,
        IsBeginnerVisible: true,
        Config: new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Signal,
            StrategyName = "VWAP Intraday Pullback Long",
            Exchange = "Hyperliquid",
            Market = "BTCUSDT",
            Timeframe = "1m",
            Direction = Direction.Long,
            Enabled = true,
            TemplateId = "vwap-intraday-pullback-long",
            TrendFilter = new TrendFilterConfig
            {
                Enabled = true,
                Type = TrendFilterType.PriceAboveEma,
                Period = 9,
                FastPeriod = 0,
                SlowPeriod = 0,
                Operator = TrendOperator.Above,
                AppliesTo = Direction.Long,
            },
            EntryLogic = EntryLogicEnum.All,
            EntryConditions =
            [
                new EntryConditionConfig
                {
                    Id = "bullish-continuation",
                    Enabled = true,
                    Type = EntryConditionType.CandlePattern,
                    Label = "Bullish continuation pattern",
                    Params = new CandlePatternParams
                    {
                        Pattern = "bullish_continuation",
                    },
                },
            ],
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.FixedPercent,
                    Value = 0.8m,
                },
                StopLoss = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.SwingLow,
                    Lookback = 10,
                },
                ExitOnOppositeSignal = false,
            },
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                PositionSizeValue = 0m,
                RiskPerTradePercent = 0.5m,
                Leverage = 1m,
                MaxOpenTrades = 2,
                CooldownValue = 3,
                CooldownUnit = CooldownUnit.Candles,
                AllowSameCandleReentry = false,
            },
            Metadata = new StrategyMetadata
            {
                Tags = ["daytrading", "vwap", "intraday"],
                Notes = "Simplified from VWAP-based strategy. VWAP filter not yet supported — uses EMA 9 as proxy for intraday trend support.",
            },
            Source = new SourceMetadata
            {
                EntryPoint = StrategyEntryPoint.Migration,
                Summary = "Built-in starter template: VWAP Intraday Pullback Long",
            },
        });

    private static TemplateDefinition RangeReversionRsiLong() => new(
        Slug: "range-reversion-rsi-long",
        Name: "Range Reversion RSI Long",
        Description: "Buy at support in a sideways range. Enters when RSI is oversold and price is near support with a bullish rejection candle. Works best in low-volatility ranging markets.",
        Tags: ["range", "mean_reversion", "rsi"],
        SortOrder: 3,
        IsBeginnerVisible: false,
        Config: new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Signal,
            StrategyName = "Range Reversion RSI Long",
            Exchange = "Hyperliquid",
            Market = "BTCUSDT",
            Timeframe = "5m",
            Direction = Direction.Long,
            Enabled = true,
            TemplateId = "range-reversion-rsi-long",
            TrendFilter = new TrendFilterConfig
            {
                Enabled = false,
                Type = TrendFilterType.PriceAboveEma,
                Period = 50,
                FastPeriod = 0,
                SlowPeriod = 0,
                Operator = TrendOperator.Above,
                AppliesTo = Direction.Long,
            },
            EntryLogic = EntryLogicEnum.All,
            EntryConditions =
            [
                new EntryConditionConfig
                {
                    Id = "rsi-oversold",
                    Enabled = true,
                    Type = EntryConditionType.Rsi,
                    Label = "RSI(14) \u2264 30 (oversold)",
                    Params = new RsiParams
                    {
                        Period = 14,
                        Operator = "lte",
                        Value = 30m,
                    },
                },
                new EntryConditionConfig
                {
                    Id = "near-support",
                    Enabled = true,
                    Type = EntryConditionType.SupportResistance,
                    Label = "Price near support level",
                    Params = new SupportResistanceParams
                    {
                        Lookback = 20,
                        Strength = 3,
                        Operator = "near_support",
                        Tolerance = 0.5m,
                    },
                },
                new EntryConditionConfig
                {
                    Id = "bullish-rejection",
                    Enabled = true,
                    Type = EntryConditionType.CandlePattern,
                    Label = "Bullish rejection candle",
                    Params = new CandlePatternParams
                    {
                        Pattern = "bullish_rejection",
                    },
                },
            ],
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.FixedPercent,
                    Value = 1.4m,
                },
                StopLoss = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.FixedPercent,
                    Value = 1.0m,
                },
                ExitOnOppositeSignal = false,
            },
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                PositionSizeValue = 0m,
                RiskPerTradePercent = 0.75m,
                Leverage = 1m,
                MaxOpenTrades = 1,
                CooldownValue = 3,
                CooldownUnit = CooldownUnit.Candles,
                AllowSameCandleReentry = false,
            },
            Metadata = new StrategyMetadata
            {
                Tags = ["range", "mean_reversion", "rsi"],
                Notes = "Range detection is manual — use this strategy in sideways, low-volatility markets only. The support/resistance filter approximates range boundaries.",
            },
            Source = new SourceMetadata
            {
                EntryPoint = StrategyEntryPoint.Migration,
                Summary = "Built-in starter template: Range Reversion RSI Long",
            },
        });

    private static TemplateDefinition BtcDcaBelow90000() => new(
        Slug: "btc-dca-below-90000",
        Name: "BTC DCA Below $90,000",
        Description: "Staged accumulation of BTC when price trades at or below $90,000. Places $50 orders at 1.5% intervals down to a $400 budget cap. Pauses below $75,000 for safety.",
        Tags: ["dca", "accumulator"],
        SortOrder: 4,
        IsBeginnerVisible: false,
        Config: new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Dca,
            StrategyName = "BTC DCA Below $90,000",
            Exchange = "Hyperliquid",
            Market = "BTCUSDT",
            Timeframe = "5m",
            Direction = Direction.Long,
            Enabled = true,
            TemplateId = "btc-dca-below-90000",
            Dca = new DcaConfig
            {
                Interval = DcaInterval.FiveMinutes,
                TimeOfDayUtc = "00:00",
                BaseAmountUsd = 50m,
                Allocations =
                [
                    new DcaAllocation { Market = "BTCUSDT", WeightPercent = 100m },
                ],
                GateConditions = new DcaGateConfig
                {
                    MaxPriceUsd = 90000m,
                },
                ScalingBands =
                [
                    new DcaScalingBand
                    {
                        PriceUpperUsd = 90000m,
                        PriceLowerUsd = 75000m,
                        ScalingPercent = 100m,
                    },
                ],
                BudgetCapUsd = 400m,
                ProfitTaking = new DcaProfitTakingConfig
                {
                    Tiers =
                    [
                        new DcaProfitTier { TargetMultiple = 1.04m, SellPercent = 100m },
                    ],
                },
            },
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.FixedPercent,
                    Value = 4.0m,
                },
                StopLoss = new ExitRuleConfig
                {
                    Enabled = false,
                    Type = ExitRuleType.FixedPercent,
                },
                ExitOnOppositeSignal = false,
            },
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.FixedNotional,
                PositionSizeValue = 50m,
                Leverage = 1m,
                MaxOpenTrades = 8,
                CooldownValue = 0,
                CooldownUnit = CooldownUnit.Candles,
                AllowSameCandleReentry = false,
            },
            Metadata = new StrategyMetadata
            {
                Tags = ["dca", "accumulator"],
                Notes = "Accumulate BTC below $90,000 with staged buys. Automatically pauses below $75,000 to avoid catching a falling knife.",
            },
            Source = new SourceMetadata
            {
                EntryPoint = StrategyEntryPoint.Migration,
                Summary = "Built-in starter template: BTC DCA Below $90,000",
            },
        });

    private sealed record TemplateDefinition(
        string Slug,
        string Name,
        string Description,
        string[] Tags,
        int SortOrder,
        bool IsBeginnerVisible,
        StrategyConfig Config);
}
