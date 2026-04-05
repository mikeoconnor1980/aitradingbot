namespace TradingApp.Application.Optimization.Models;

public sealed record ParameterBounds
{
    public decimal StopLossMin { get; init; } = 1m;
    public decimal StopLossMax { get; init; } = 5m;
    public decimal StopLossStep { get; init; } = 0.5m;

    public decimal TakeProfitMin { get; init; } = 2m;
    public decimal TakeProfitMax { get; init; } = 10m;
    public decimal TakeProfitStep { get; init; } = 1m;

    public decimal LeverageMin { get; init; } = 3m;
    public decimal LeverageMax { get; init; } = 10m;
    public decimal LeverageStep { get; init; } = 1m;

    public decimal[] PositionSizeOptions { get; init; } = [10m, 15m, 20m];
    public int[] RsiPeriods { get; init; } = [7, 14, 21];
    public decimal[] RsiThresholds { get; init; } = [30m, 35m, 40m, 45m];
    public int[] MacdFastPeriods { get; init; } = [8, 12, 16];
    public int[] MacdSlowPeriods { get; init; } = [21, 26, 30];
    public int[] MacdSignalPeriods { get; init; } = [9];
    public int[] EmaPeriods { get; init; } = [20, 50, 100];
    public decimal[] EmaProximityPercents { get; init; } = [0.15m, 0.25m, 0.5m];
    public bool IncludeTrendFilter { get; init; } = true;
    public int[][] TrendFilterPairs { get; init; } = [[20, 50], [50, 200]];
}