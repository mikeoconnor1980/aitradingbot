using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class RunBacktestRequest
{
    [Required]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string[] Intervals { get; set; } = [];

    [Required]
    public DateTime? StartDate { get; set; }

    [Required]
    public DateTime? EndDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "initialCapital must be > 0")]
    public decimal? InitialCapital { get; set; }

    [Required]
    public GridStrategyConfigRequest StrategyConfig { get; set; } = null!;
}

public sealed class GridStrategyConfigRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "gridLevels must be > 0")]
    public int GridLevels { get; set; }

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "gridSpacing must be > 0")]
    public decimal GridSpacing { get; set; }

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "takeProfitPercent must be > 0")]
    public decimal TakeProfitPercent { get; set; }

    [Required]
    public decimal BreakdownThreshold { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "makerFee must be >= 0")]
    public decimal MakerFee { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "takerFee must be >= 0")]
    public decimal TakerFee { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "slippage must be >= 0")]
    public decimal Slippage { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "positionSize must be > 0")]
    public decimal PositionSize { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "leverage must be > 0")]
    public decimal Leverage { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "stopLossPercent must be > 0")]
    public decimal StopLossPercent { get; set; }
}