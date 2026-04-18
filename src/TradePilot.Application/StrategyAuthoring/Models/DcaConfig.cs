namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record DcaConfig
{
    public DcaInterval Interval { get; init; } = DcaInterval.Weekly;
    public int? DayOfWeek { get; init; }
    public int? DayOfMonth { get; init; }
    public string TimeOfDayUtc { get; init; } = "00:00";
    public decimal BaseAmountUsd { get; init; }
    public IReadOnlyList<DcaAllocation> Allocations { get; init; } = [];
    public DcaGateConfig? GateConditions { get; init; }
    public IReadOnlyList<DcaScalingBand>? ScalingBands { get; init; }
    public DcaProfitTakingConfig? ProfitTaking { get; init; }
    public decimal? BudgetCapUsd { get; init; }
}