using System.ComponentModel.DataAnnotations;

namespace TradePilot.Api.Models;

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

    [Range(10, int.MaxValue)]
    public int SampleSize { get; set; } = 500;

    // --- Parameter Bounds ---
    public string[]? Directions { get; set; }
    public string[]? Timeframes { get; set; }
    public decimal? StopLossMin { get; set; }
    public decimal? StopLossMax { get; set; }
    public decimal? TakeProfitMin { get; set; }
    public decimal? TakeProfitMax { get; set; }
    public decimal? LeverageMin { get; set; }
    public decimal? LeverageMax { get; set; }
    public decimal? PositionSizePercent { get; set; }
    public string? PositionSizeMode { get; set; }
    public decimal[]? RiskPerTradePercentOptions { get; set; }
    public bool? IncludeAutoLeverage { get; set; }

    // --- Signal Operators ---
    public string[]? RsiOperators { get; set; }
    public int[]? RsiPeriods { get; set; }
    public decimal[]? RsiThresholds { get; set; }
    public string[]? MacdOperators { get; set; }
    public int[]? MacdFastPeriods { get; set; }
    public int[]? MacdSlowPeriods { get; set; }
    public string[]? PriceVsEmaOperators { get; set; }
    public int[]? EmaPeriods { get; set; }
    public decimal[]? EmaProximityPercents { get; set; }

    // --- Exit / Risk options ---
    public bool? ExitOnOppositeSignal { get; set; }
    public int[]? MaxOpenTradesOptions { get; set; }
    public int[]? CooldownCandlesOptions { get; set; }

    // --- Trend Filter ---
    public bool? IncludeTrendFilter { get; set; }

    // --- Walk-Forward Validation ---
    public bool? WalkForwardEnabled { get; set; }
    public decimal? WalkForwardSplitPercent { get; set; }

    // --- Evolutionary Optimization ---
    public bool? EvolutionaryEnabled { get; set; }
    public int? EvolutionaryGenerations { get; set; }
    public int? EvolutionaryEliteCount { get; set; }
    public decimal? EvolutionaryMutationRate { get; set; }
    public decimal? EvolutionaryCrossoverRate { get; set; }

    // --- Fitness Thresholds ---
    public decimal? MinWinRate { get; set; }
    public int? MinTotalTrades { get; set; }
    public decimal? MaxDrawdownPercent { get; set; }
}