using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class RunOptimizationRequest
{
    [Required]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    public DateTime? StartDate { get; set; }

    [Required]
    public DateTime? EndDate { get; set; }

    [Range(100, 1_000_000)]
    public decimal InitialCapital { get; set; } = 10_000m;

    [Range(10, 5_000)]
    public int SampleSize { get; set; } = 500;

    public decimal? StopLossMin { get; set; }
    public decimal? StopLossMax { get; set; }
    public decimal? TakeProfitMin { get; set; }
    public decimal? TakeProfitMax { get; set; }
    public decimal? LeverageMin { get; set; }
    public decimal? LeverageMax { get; set; }
    public decimal? MinWinRate { get; set; }
    public int? MinTotalTrades { get; set; }
    public decimal? MaxDrawdownPercent { get; set; }
}