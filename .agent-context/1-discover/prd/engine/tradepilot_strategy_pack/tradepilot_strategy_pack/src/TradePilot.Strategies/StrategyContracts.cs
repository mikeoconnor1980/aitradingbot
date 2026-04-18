using System;
using System.Collections.Generic;

namespace TradePilot.Strategies;

public enum StrategyType
{
    Signal,
    Dca,
    Grid
}

public enum StrategyDirection
{
    Long,
    Short,
    Neutral
}

public sealed record MarketConfig(
    string Symbol,
    string Venue,
    string? BaseAsset = null,
    string? QuoteAsset = null
);

public sealed record ExecutionConfig(
    string EntryType,
    int MaxReentries,
    IReadOnlyList<string>? AllowedSessions = null,
    decimal? MaxSpreadBps = null,
    decimal? MaxSlippageBps = null,
    bool? FlatBySessionEnd = null
);

public sealed record PositionSizingConfig(
    string Mode,
    decimal? Value = null,
    string? Currency = null,
    decimal? RiskPercent = null
);

public sealed record StopLossConfig(
    string Mode,
    decimal? Value = null,
    string? Reference = null,
    string? ActionOnHit = null
);

public sealed record TakeProfitConfig(
    string Mode,
    decimal? RrTarget = null,
    decimal? TargetPercent = null,
    IReadOnlyList<Dictionary<string, object?>>? Targets = null
);

public sealed record ExposureCapConfig(
    decimal Value,
    string Currency
);

public sealed record RiskConfig(
    PositionSizingConfig PositionSizing,
    StopLossConfig StopLoss,
    TakeProfitConfig TakeProfit,
    ExposureCapConfig? MaxTotalExposure = null
);

public sealed record TelemetryConfig(
    bool EmitSignals,
    bool EmitOrders,
    bool EmitPositionUpdates,
    bool EmitPnlUpdates,
    IReadOnlyList<string>? CustomTags = null
);

public sealed record IndicatorDefinition(
    string Id,
    string Kind,
    string Timeframe,
    IReadOnlyDictionary<string, object?> Params
);

public sealed record SignalDefinition(
    string Id,
    string Kind,
    string? Timeframe,
    IReadOnlyDictionary<string, object?> Source
);

public sealed record ValueRef(
    string Type,
    object? Value = null,
    string? Id = null,
    string? Path = null
);

public sealed record ConditionDefinition(
    string Lhs,
    string Operator,
    ValueRef Rhs,
    string? Timeframe = null,
    int? LookbackBars = null,
    string? Notes = null
);

public abstract record StrategyBase
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required StrategyType StrategyType { get; init; }
    public required StrategyDirection Direction { get; init; }
    public required bool Enabled { get; init; }
    public required MarketConfig Market { get; init; }
    public required ExecutionConfig Execution { get; init; }
    public required RiskConfig Risk { get; init; }
    public required TelemetryConfig Telemetry { get; init; }

    public IReadOnlyDictionary<string, string>? Timeframes { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<IndicatorDefinition> Indicators { get; init; } = Array.Empty<IndicatorDefinition>();
    public IReadOnlyList<SignalDefinition> Signals { get; init; } = Array.Empty<SignalDefinition>();
    public IReadOnlyList<ConditionDefinition> EnablementConditions { get; init; } = Array.Empty<ConditionDefinition>();
    public IReadOnlyList<ConditionDefinition> DisablementConditions { get; init; } = Array.Empty<ConditionDefinition>();
}

public sealed record SignalCoreConfig(
    IReadOnlyList<ConditionDefinition> MarketBias,
    IReadOnlyList<ConditionDefinition> Setup,
    IReadOnlyList<ConditionDefinition> EntryTrigger
);

public sealed record SignalScoringConfig(
    int? MinScore,
    IReadOnlyDictionary<string, int>? Weights
);

public sealed record SignalFiltersConfig(
    IReadOnlyList<ConditionDefinition>? Hard,
    IReadOnlyList<ConditionDefinition>? Soft,
    SignalScoringConfig? Scoring
);

public sealed record SignalStrategy : StrategyBase
{
    public required SignalCoreConfig Core { get; init; }
    public required SignalFiltersConfig Filters { get; init; }
}

public sealed record DcaTriggerConfig(
    IReadOnlyList<ConditionDefinition> Activation
);

public sealed record DcaBaseOrderSize(
    decimal Value,
    string Currency
);

public sealed record DcaScalingConfig(
    string Mode,
    decimal? StepPercent,
    int MaxOrders,
    decimal? SizeMultiplier = null
);

public sealed record DcaPlacementConfig(
    string ReferencePrice,
    IReadOnlyList<Dictionary<string, object?>> PlaceOrders
);

public sealed record DcaLadderConfig(
    string OrderSizingMode,
    DcaBaseOrderSize BaseOrderSize,
    DcaScalingConfig Scaling,
    DcaPlacementConfig Placement
);

public sealed record DcaBudgetConfig(
    ExposureCapConfig MaxTotalInvestment
);

public sealed record DcaStrategy : StrategyBase
{
    public required DcaTriggerConfig Trigger { get; init; }
    public required DcaLadderConfig Ladder { get; init; }
    public DcaBudgetConfig? Budget { get; init; }
}

public sealed record GridOrderSizeConfig(
    decimal Value,
    string Currency
);

public sealed record GridConfig(
    decimal LowerBound,
    decimal UpperBound,
    int GridCount,
    string SpacingMode,
    string OrderSizeMode,
    GridOrderSizeConfig OrderSize
);

public sealed record InventoryConfig(
    Dictionary<string, object?>? InitialBaseAllocation,
    bool RebalanceOnStart,
    bool? MaintainInventoryBuffer = null
);

public sealed record ProfitModelConfig(
    bool CapturePerGridFill,
    bool FeeAdjusted
);

public sealed record GridRebalanceConfig(
    bool? AutoRecenter,
    decimal? RecenterThresholdPercent,
    string? RecenterAction
);

public sealed record GridStrategy : StrategyBase
{
    public required IReadOnlyList<ConditionDefinition> Activation { get; init; }
    public required GridConfig Grid { get; init; }
    public required InventoryConfig Inventory { get; init; }
    public required ProfitModelConfig ProfitModel { get; init; }
    public GridRebalanceConfig? Rebalance { get; init; }
}