using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace TradePilot.Strategies.Parsing;

public sealed class StrategyFileYaml
{
    [YamlMember(Alias = "strategies")]
    public List<Dictionary<string, object?>> Strategies { get; init; } = new();
}

public abstract class StrategyYamlBase
{
    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    [YamlMember(Alias = "name")]
    public string Name { get; init; } = string.Empty;

    [YamlMember(Alias = "version")]
    public string Version { get; init; } = "1.0.0";

    [YamlMember(Alias = "strategy_type")]
    public string StrategyType { get; init; } = string.Empty;

    [YamlMember(Alias = "direction")]
    public string Direction { get; init; } = string.Empty;

    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; init; }

    [YamlMember(Alias = "tags")]
    public List<string>? Tags { get; init; }

    [YamlMember(Alias = "description")]
    public string? Description { get; init; }

    [YamlMember(Alias = "market")]
    public MarketYaml? Market { get; init; }

    [YamlMember(Alias = "timeframes")]
    public Dictionary<string, string>? Timeframes { get; init; }

    [YamlMember(Alias = "indicators")]
    public List<IndicatorYaml>? Indicators { get; init; }

    [YamlMember(Alias = "signals")]
    public List<SignalYaml>? Signals { get; init; }

    [YamlMember(Alias = "enablement_conditions")]
    public List<ConditionYaml>? EnablementConditions { get; init; }

    [YamlMember(Alias = "disablement_conditions")]
    public List<ConditionYaml>? DisablementConditions { get; init; }

    [YamlMember(Alias = "execution")]
    public ExecutionYaml? Execution { get; init; }

    [YamlMember(Alias = "risk")]
    public RiskYaml? Risk { get; init; }

    [YamlMember(Alias = "telemetry")]
    public TelemetryYaml? Telemetry { get; init; }
}

public sealed class SignalStrategyYaml : StrategyYamlBase
{
    [YamlMember(Alias = "core")]
    public SignalCoreYaml? Core { get; init; }

    [YamlMember(Alias = "filters")]
    public SignalFiltersYaml? Filters { get; init; }
}

public sealed class DcaStrategyYaml : StrategyYamlBase
{
    [YamlMember(Alias = "trigger")]
    public DcaTriggerYaml? Trigger { get; init; }

    [YamlMember(Alias = "ladder")]
    public DcaLadderYaml? Ladder { get; init; }

    [YamlMember(Alias = "budget")]
    public DcaBudgetYaml? Budget { get; init; }
}

public sealed class GridStrategyYaml : StrategyYamlBase
{
    [YamlMember(Alias = "activation")]
    public List<ConditionYaml>? Activation { get; init; }

    [YamlMember(Alias = "grid")]
    public GridYaml? Grid { get; init; }

    [YamlMember(Alias = "inventory")]
    public InventoryYaml? Inventory { get; init; }

    [YamlMember(Alias = "profit_model")]
    public ProfitModelYaml? ProfitModel { get; init; }

    [YamlMember(Alias = "rebalance")]
    public GridRebalanceYaml? Rebalance { get; init; }
}

public sealed class MarketYaml
{
    [YamlMember(Alias = "symbol")]
    public string Symbol { get; init; } = string.Empty;

    [YamlMember(Alias = "venue")]
    public string Venue { get; init; } = string.Empty;

    [YamlMember(Alias = "base_asset")]
    public string? BaseAsset { get; init; }

    [YamlMember(Alias = "quote_asset")]
    public string? QuoteAsset { get; init; }
}

public sealed class IndicatorYaml
{
    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    [YamlMember(Alias = "kind")]
    public string Kind { get; init; } = string.Empty;

    [YamlMember(Alias = "timeframe")]
    public string Timeframe { get; init; } = string.Empty;

    [YamlMember(Alias = "params")]
    public Dictionary<string, object?> Params { get; init; } = new();
}

public sealed class SignalYaml
{
    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    [YamlMember(Alias = "kind")]
    public string Kind { get; init; } = string.Empty;

    [YamlMember(Alias = "timeframe")]
    public string? Timeframe { get; init; }

    [YamlMember(Alias = "source")]
    public Dictionary<string, object?> Source { get; init; } = new();
}

public sealed class ConditionYaml
{
    [YamlMember(Alias = "lhs")]
    public string Lhs { get; init; } = string.Empty;

    [YamlMember(Alias = "operator")]
    public string Operator { get; init; } = string.Empty;

    [YamlMember(Alias = "rhs")]
    public ValueRefYaml? Rhs { get; init; }

    [YamlMember(Alias = "timeframe")]
    public string? Timeframe { get; init; }

    [YamlMember(Alias = "lookback_bars")]
    public int? LookbackBars { get; init; }

    [YamlMember(Alias = "notes")]
    public string? Notes { get; init; }
}

public sealed class ValueRefYaml
{
    [YamlMember(Alias = "type")]
    public string Type { get; init; } = string.Empty;

    [YamlMember(Alias = "value")]
    public object? Value { get; init; }

    [YamlMember(Alias = "id")]
    public string? Id { get; init; }

    [YamlMember(Alias = "path")]
    public string? Path { get; init; }
}

public sealed class ExecutionYaml
{
    [YamlMember(Alias = "entry_type")]
    public string EntryType { get; init; } = string.Empty;

    [YamlMember(Alias = "max_reentries")]
    public int MaxReentries { get; init; }

    [YamlMember(Alias = "allowed_sessions")]
    public List<string>? AllowedSessions { get; init; }

    [YamlMember(Alias = "max_spread_bps")]
    public decimal? MaxSpreadBps { get; init; }

    [YamlMember(Alias = "max_slippage_bps")]
    public decimal? MaxSlippageBps { get; init; }

    [YamlMember(Alias = "flat_by_session_end")]
    public bool? FlatBySessionEnd { get; init; }
}

public sealed class RiskYaml
{
    [YamlMember(Alias = "position_sizing")]
    public PositionSizingYaml? PositionSizing { get; init; }

    [YamlMember(Alias = "stop_loss")]
    public StopLossYaml? StopLoss { get; init; }

    [YamlMember(Alias = "take_profit")]
    public TakeProfitYaml? TakeProfit { get; init; }

    [YamlMember(Alias = "max_total_exposure")]
    public ExposureCapYaml? MaxTotalExposure { get; init; }
}

public sealed class PositionSizingYaml
{
    [YamlMember(Alias = "mode")]
    public string Mode { get; init; } = string.Empty;

    [YamlMember(Alias = "value")]
    public decimal? Value { get; init; }

    [YamlMember(Alias = "currency")]
    public string? Currency { get; init; }

    [YamlMember(Alias = "risk_percent")]
    public decimal? RiskPercent { get; init; }
}

public sealed class StopLossYaml
{
    [YamlMember(Alias = "mode")]
    public string Mode { get; init; } = string.Empty;

    [YamlMember(Alias = "value")]
    public decimal? Value { get; init; }

    [YamlMember(Alias = "reference")]
    public string? Reference { get; init; }

    [YamlMember(Alias = "action_on_hit")]
    public string? ActionOnHit { get; init; }
}

public sealed class TakeProfitYaml
{
    [YamlMember(Alias = "mode")]
    public string Mode { get; init; } = string.Empty;

    [YamlMember(Alias = "rr_target")]
    public decimal? RrTarget { get; init; }

    [YamlMember(Alias = "target_percent")]
    public decimal? TargetPercent { get; init; }

    [YamlMember(Alias = "targets")]
    public List<Dictionary<string, object?>>? Targets { get; init; }
}

public sealed class ExposureCapYaml
{
    [YamlMember(Alias = "value")]
    public decimal Value { get; init; }

    [YamlMember(Alias = "currency")]
    public string Currency { get; init; } = string.Empty;
}

public sealed class TelemetryYaml
{
    [YamlMember(Alias = "emit_signals")]
    public bool EmitSignals { get; init; }

    [YamlMember(Alias = "emit_orders")]
    public bool EmitOrders { get; init; }

    [YamlMember(Alias = "emit_position_updates")]
    public bool EmitPositionUpdates { get; init; }

    [YamlMember(Alias = "emit_pnl_updates")]
    public bool EmitPnlUpdates { get; init; }

    [YamlMember(Alias = "custom_tags")]
    public List<string>? CustomTags { get; init; }
}

public sealed class SignalCoreYaml
{
    [YamlMember(Alias = "market_bias")]
    public List<ConditionYaml>? MarketBias { get; init; }

    [YamlMember(Alias = "setup")]
    public List<ConditionYaml>? Setup { get; init; }

    [YamlMember(Alias = "entry_trigger")]
    public List<ConditionYaml>? EntryTrigger { get; init; }
}

public sealed class SignalFiltersYaml
{
    [YamlMember(Alias = "hard")]
    public List<ConditionYaml>? Hard { get; init; }

    [YamlMember(Alias = "soft")]
    public List<ConditionYaml>? Soft { get; init; }

    [YamlMember(Alias = "scoring")]
    public SignalScoringYaml? Scoring { get; init; }
}

public sealed class SignalScoringYaml
{
    [YamlMember(Alias = "min_score")]
    public int? MinScore { get; init; }

    [YamlMember(Alias = "weights")]
    public Dictionary<string, int>? Weights { get; init; }
}

public sealed class DcaTriggerYaml
{
    [YamlMember(Alias = "activation")]
    public List<ConditionYaml>? Activation { get; init; }
}

public sealed class DcaLadderYaml
{
    [YamlMember(Alias = "order_sizing_mode")]
    public string OrderSizingMode { get; init; } = string.Empty;

    [YamlMember(Alias = "base_order_size")]
    public DcaBaseOrderSizeYaml? BaseOrderSize { get; init; }

    [YamlMember(Alias = "scaling")]
    public DcaScalingYaml? Scaling { get; init; }

    [YamlMember(Alias = "placement")]
    public DcaPlacementYaml? Placement { get; init; }
}

public sealed class DcaBaseOrderSizeYaml
{
    [YamlMember(Alias = "value")]
    public decimal Value { get; init; }

    [YamlMember(Alias = "currency")]
    public string Currency { get; init; } = string.Empty;
}

public sealed class DcaScalingYaml
{
    [YamlMember(Alias = "mode")]
    public string Mode { get; init; } = string.Empty;

    [YamlMember(Alias = "step_percent")]
    public decimal? StepPercent { get; init; }

    [YamlMember(Alias = "max_orders")]
    public int MaxOrders { get; init; }

    [YamlMember(Alias = "size_multiplier")]
    public decimal? SizeMultiplier { get; init; }
}

public sealed class DcaPlacementYaml
{
    [YamlMember(Alias = "reference_price")]
    public string ReferencePrice { get; init; } = string.Empty;

    [YamlMember(Alias = "place_orders")]
    public List<Dictionary<string, object?>> PlaceOrders { get; init; } = new();
}

public sealed class DcaBudgetYaml
{
    [YamlMember(Alias = "max_total_investment")]
    public ExposureCapYaml? MaxTotalInvestment { get; init; }
}

public sealed class GridYaml
{
    [YamlMember(Alias = "lower_bound")]
    public decimal LowerBound { get; init; }

    [YamlMember(Alias = "upper_bound")]
    public decimal UpperBound { get; init; }

    [YamlMember(Alias = "grid_count")]
    public int GridCount { get; init; }

    [YamlMember(Alias = "spacing_mode")]
    public string SpacingMode { get; init; } = string.Empty;

    [YamlMember(Alias = "order_size_mode")]
    public string OrderSizeMode { get; init; } = string.Empty;

    [YamlMember(Alias = "order_size")]
    public GridOrderSizeYaml? OrderSize { get; init; }
}

public sealed class GridOrderSizeYaml
{
    [YamlMember(Alias = "value")]
    public decimal Value { get; init; }

    [YamlMember(Alias = "currency")]
    public string Currency { get; init; } = string.Empty;
}

public sealed class InventoryYaml
{
    [YamlMember(Alias = "initial_base_allocation")]
    public Dictionary<string, object?>? InitialBaseAllocation { get; init; }

    [YamlMember(Alias = "rebalance_on_start")]
    public bool RebalanceOnStart { get; init; }

    [YamlMember(Alias = "maintain_inventory_buffer")]
    public bool? MaintainInventoryBuffer { get; init; }
}

public sealed class ProfitModelYaml
{
    [YamlMember(Alias = "capture_per_grid_fill")]
    public bool CapturePerGridFill { get; init; }

    [YamlMember(Alias = "fee_adjusted")]
    public bool FeeAdjusted { get; init; }
}

public sealed class GridRebalanceYaml
{
    [YamlMember(Alias = "auto_recenter")]
    public bool? AutoRecenter { get; init; }

    [YamlMember(Alias = "recenter_threshold_percent")]
    public decimal? RecenterThresholdPercent { get; init; }

    [YamlMember(Alias = "recenter_action")]
    public string? RecenterAction { get; init; }
}