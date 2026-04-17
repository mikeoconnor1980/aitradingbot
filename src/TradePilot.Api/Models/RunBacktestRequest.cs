using System.ComponentModel.DataAnnotations;

namespace TradePilot.Api.Models;

public sealed class RunBacktestRequest : IValidatableObject
{
    public string? Symbol { get; set; }

    public string[]? Intervals { get; set; }

    [Required]
    public DateTime? StartDate { get; set; }

    [Required]
    public DateTime? EndDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "initialCapital must be > 0")]
    public decimal? InitialCapital { get; set; }

    public StrategyConfigRequest? StrategyConfig { get; set; }

    [Required]
    public ExecutionConfigRequest ExecutionConfig { get; set; } = null!;

    public bool EnableAuditLog { get; set; } = true;
    public Guid? StrategyId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StrategyId.HasValue)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(Symbol))
        {
            yield return new ValidationResult("The Symbol field is required.", [nameof(Symbol)]);
        }

        if (Intervals is null || Intervals.Length == 0)
        {
            yield return new ValidationResult(
                "The Intervals field must contain at least one interval.",
                [nameof(Intervals)]);
        }
    }
}

public sealed class StrategyConfigRequest
{
    [Required]
    public string StrategyName { get; set; } = string.Empty;

    [Required]
    public string Market { get; set; } = string.Empty;

    public string Timeframe { get; set; } = "15m";

    public string Direction { get; set; } = "long";

    public bool Enabled { get; set; } = true;

    public GridConfigRequest? Grid { get; set; }

    [Required]
    public ExitConfigRequest Exit { get; set; } = null!;

    [Required]
    public RiskConfigRequest Risk { get; set; } = null!;
}

public sealed class GridConfigRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "gridLevels must be > 0")]
    public int Levels { get; set; }

    [Required]
    public string EntryMode { get; set; } = "auto_from_signal_candle";

    [Range(0.00000001, double.MaxValue, ErrorMessage = "manualAnchorPrice must be > 0")]
    public decimal? AnchorPrice { get; set; }

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "gridSpacing must be > 0")]
    public decimal Spacing { get; set; }

    [Required]
    public decimal BreakdownThreshold { get; set; }
}

public sealed class ExitConfigRequest
{
    [Required]
    public ExitRuleRequest TakeProfit { get; set; } = new();

    [Required]
    public ExitRuleRequest StopLoss { get; set; } = new();

    public bool ExitOnOppositeSignal { get; set; }
}

public sealed class ExitRuleRequest
{
    public bool Enabled { get; set; }
    public string Type { get; set; } = "fixed_percent";
    public decimal? Value { get; set; }
    public int? Lookback { get; set; }
}

public sealed class RiskConfigRequest
{
    public string PositionSizeType { get; set; } = "percent_wallet";

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "positionSizeValue must be > 0")]
    public decimal PositionSizeValue { get; set; }

    [Range(0.01, 100, ErrorMessage = "riskPerTradePercent must be between 0.01 and 100")]
    public decimal? RiskPerTradePercent { get; set; }

    [Required]
    [Range(1, double.MaxValue, ErrorMessage = "leverage must be >= 1")]
    public decimal Leverage { get; set; } = 1m;

    public bool AutoLeverage { get; set; }

    public int MaxOpenTrades { get; set; } = 1;
    public int CooldownValue { get; set; }
    public string CooldownUnit { get; set; } = "candles";
    public bool AllowSameCandleReentry { get; set; }
}

public sealed class ExecutionConfigRequest
{
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "makerFee must be >= 0")]
    public decimal MakerFee { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "takerFee must be >= 0")]
    public decimal TakerFee { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "slippage must be >= 0")]
    public decimal Slippage { get; set; }
}