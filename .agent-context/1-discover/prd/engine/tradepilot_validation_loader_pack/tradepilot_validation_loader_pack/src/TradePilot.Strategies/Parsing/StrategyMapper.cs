using System;
using System.Collections.Generic;
using System.Linq;
using TradePilot.Strategies.Parsing;

namespace TradePilot.Strategies.Parsing;

public static class StrategyMapper
{
    public static StrategyBase Map(StrategyYamlBase source)
        => source switch
        {
            SignalStrategyYaml x => MapSignal(x),
            DcaStrategyYaml x => MapDca(x),
            GridStrategyYaml x => MapGrid(x),
            _ => throw new NotSupportedException($"Unsupported YAML strategy type: {source.GetType().Name}")
        };

    public static SignalStrategy MapSignal(SignalStrategyYaml x)
        => new()
        {
            Id = x.Id,
            Name = x.Name,
            Version = x.Version,
            StrategyType = ParseStrategyType(x.StrategyType),
            Direction = ParseDirection(x.Direction),
            Enabled = x.Enabled,
            Tags = x.Tags,
            Description = x.Description,
            Market = MapMarket(x.Market!),
            Timeframes = x.Timeframes,
            Indicators = MapIndicators(x.Indicators),
            Signals = MapSignals(x.Signals),
            EnablementConditions = MapConditions(x.EnablementConditions),
            DisablementConditions = MapConditions(x.DisablementConditions),
            Execution = MapExecution(x.Execution!),
            Risk = MapRisk(x.Risk!),
            Telemetry = MapTelemetry(x.Telemetry!),
            Core = new SignalCoreConfig(
                MapConditions(x.Core!.MarketBias),
                MapConditions(x.Core!.Setup),
                MapConditions(x.Core!.EntryTrigger)
            ),
            Filters = new SignalFiltersConfig(
                MapConditions(x.Filters?.Hard),
                MapConditions(x.Filters?.Soft),
                x.Filters?.Scoring is null
                    ? null
                    : new SignalScoringConfig(x.Filters.Scoring.MinScore, x.Filters.Scoring.Weights)
            )
        };

    public static DcaStrategy MapDca(DcaStrategyYaml x)
        => new()
        {
            Id = x.Id,
            Name = x.Name,
            Version = x.Version,
            StrategyType = ParseStrategyType(x.StrategyType),
            Direction = ParseDirection(x.Direction),
            Enabled = x.Enabled,
            Tags = x.Tags,
            Description = x.Description,
            Market = MapMarket(x.Market!),
            Timeframes = x.Timeframes,
            Indicators = MapIndicators(x.Indicators),
            Signals = MapSignals(x.Signals),
            EnablementConditions = MapConditions(x.EnablementConditions),
            DisablementConditions = MapConditions(x.DisablementConditions),
            Execution = MapExecution(x.Execution!),
            Risk = MapRisk(x.Risk!),
            Telemetry = MapTelemetry(x.Telemetry!),
            Trigger = new DcaTriggerConfig(MapConditions(x.Trigger!.Activation)),
            Ladder = new DcaLadderConfig(
                x.Ladder!.OrderSizingMode,
                new DcaBaseOrderSize(x.Ladder.BaseOrderSize!.Value, x.Ladder.BaseOrderSize.Currency),
                new DcaScalingConfig(
                    x.Ladder.Scaling!.Mode,
                    x.Ladder.Scaling.StepPercent,
                    x.Ladder.Scaling.MaxOrders,
                    x.Ladder.Scaling.SizeMultiplier
                ),
                new DcaPlacementConfig(
                    x.Ladder.Placement!.ReferencePrice,
                    x.Ladder.Placement.PlaceOrders
                )
            ),
            Budget = x.Budget?.MaxTotalInvestment is null
                ? null
                : new DcaBudgetConfig(new ExposureCapConfig(
                    x.Budget.MaxTotalInvestment.Value,
                    x.Budget.MaxTotalInvestment.Currency))
        };

    public static GridStrategy MapGrid(GridStrategyYaml x)
        => new()
        {
            Id = x.Id,
            Name = x.Name,
            Version = x.Version,
            StrategyType = ParseStrategyType(x.StrategyType),
            Direction = ParseDirection(x.Direction),
            Enabled = x.Enabled,
            Tags = x.Tags,
            Description = x.Description,
            Market = MapMarket(x.Market!),
            Timeframes = x.Timeframes,
            Indicators = MapIndicators(x.Indicators),
            Signals = MapSignals(x.Signals),
            EnablementConditions = MapConditions(x.EnablementConditions),
            DisablementConditions = MapConditions(x.DisablementConditions),
            Execution = MapExecution(x.Execution!),
            Risk = MapRisk(x.Risk!),
            Telemetry = MapTelemetry(x.Telemetry!),
            Activation = MapConditions(x.Activation),
            Grid = new GridConfig(
                x.Grid!.LowerBound,
                x.Grid.UpperBound,
                x.Grid.GridCount,
                x.Grid.SpacingMode,
                x.Grid.OrderSizeMode,
                new GridOrderSizeConfig(x.Grid.OrderSize!.Value, x.Grid.OrderSize.Currency)
            ),
            Inventory = new InventoryConfig(
                x.Inventory!.InitialBaseAllocation,
                x.Inventory.RebalanceOnStart,
                x.Inventory.MaintainInventoryBuffer
            ),
            ProfitModel = new ProfitModelConfig(
                x.ProfitModel!.CapturePerGridFill,
                x.ProfitModel.FeeAdjusted
            ),
            Rebalance = x.Rebalance is null
                ? null
                : new GridRebalanceConfig(
                    x.Rebalance.AutoRecenter,
                    x.Rebalance.RecenterThresholdPercent,
                    x.Rebalance.RecenterAction
                )
        };

    private static MarketConfig MapMarket(MarketYaml x)
        => new(x.Symbol, x.Venue, x.BaseAsset, x.QuoteAsset);

    private static ExecutionConfig MapExecution(ExecutionYaml x)
        => new(x.EntryType, x.MaxReentries, x.AllowedSessions, x.MaxSpreadBps, x.MaxSlippageBps, x.FlatBySessionEnd);

    private static RiskConfig MapRisk(RiskYaml x)
        => new(
            new PositionSizingConfig(x.PositionSizing!.Mode, x.PositionSizing.Value, x.PositionSizing.Currency, x.PositionSizing.RiskPercent),
            new StopLossConfig(x.StopLoss!.Mode, x.StopLoss.Value, x.StopLoss.Reference, x.StopLoss.ActionOnHit),
            new TakeProfitConfig(x.TakeProfit!.Mode, x.TakeProfit.RrTarget, x.TakeProfit.TargetPercent, x.TakeProfit.Targets),
            x.MaxTotalExposure is null ? null : new ExposureCapConfig(x.MaxTotalExposure.Value, x.MaxTotalExposure.Currency)
        );

    private static TelemetryConfig MapTelemetry(TelemetryYaml x)
        => new(x.EmitSignals, x.EmitOrders, x.EmitPositionUpdates, x.EmitPnlUpdates, x.CustomTags);

    private static IReadOnlyList<IndicatorDefinition> MapIndicators(List<IndicatorYaml>? values)
        => values?.Select(x => new IndicatorDefinition(x.Id, x.Kind, x.Timeframe, x.Params)).ToList()
           ?? Array.Empty<IndicatorDefinition>();

    private static IReadOnlyList<SignalDefinition> MapSignals(List<SignalYaml>? values)
        => values?.Select(x => new SignalDefinition(x.Id, x.Kind, x.Timeframe, x.Source)).ToList()
           ?? Array.Empty<SignalDefinition>();

    private static IReadOnlyList<ConditionDefinition> MapConditions(List<ConditionYaml>? values)
        => values?.Select(MapCondition).ToList()
           ?? Array.Empty<ConditionDefinition>();

    private static ConditionDefinition MapCondition(ConditionYaml x)
        => new(x.Lhs, x.Operator, MapValueRef(x.Rhs!), x.Timeframe, x.LookbackBars, x.Notes);

    private static ValueRef MapValueRef(ValueRefYaml x)
        => new(x.Type, x.Value, x.Id, x.Path);

    private static StrategyType ParseStrategyType(string value)
        => value.ToLowerInvariant() switch
        {
            "signal" => StrategyType.Signal,
            "dca" => StrategyType.Dca,
            "grid" => StrategyType.Grid,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown strategy_type.")
        };

    private static StrategyDirection ParseDirection(string value)
        => value.ToLowerInvariant() switch
        {
            "long" => StrategyDirection.Long,
            "short" => StrategyDirection.Short,
            "neutral" => StrategyDirection.Neutral,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown direction.")
        };
}