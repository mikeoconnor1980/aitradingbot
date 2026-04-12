using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.Optimization.Models;

public sealed record ParameterBounds
{
    // --- Direction ---
    public Direction[] Directions { get; init; } = [Direction.Long, Direction.Short];

    // --- Timeframe ---
    public string[] Timeframes { get; init; } = ["15m"];

    // --- Stop Loss ---
    public decimal StopLossMin { get; init; } = 1m;
    public decimal StopLossMax { get; init; } = 5m;
    public decimal StopLossStep { get; init; } = 0.5m;

    // --- Take Profit ---
    public decimal TakeProfitMin { get; init; } = 2m;
    public decimal TakeProfitMax { get; init; } = 10m;
    public decimal TakeProfitStep { get; init; } = 1m;

    // --- Leverage ---
    public decimal LeverageMin { get; init; } = 3m;
    public decimal LeverageMax { get; init; } = 10m;
    public decimal LeverageStep { get; init; } = 1m;

    // --- Position Size ---
    public PositionSizeMode PositionSizeMode { get; init; } = PositionSizeMode.PercentWallet;
    public decimal[] PositionSizeOptions { get; init; } = [10m, 15m, 20m];
    public decimal[] RiskPerTradePercentOptions { get; init; } = [0.25m, 0.5m, 1.0m, 1.5m, 2.0m, 3.0m];
    public bool IncludeAutoLeverage { get; init; } = true;

    // --- RSI ---
    public int[] RsiPeriods { get; init; } = [7, 14, 21];
    public decimal[] RsiThresholds { get; init; } = [30m, 35m, 40m, 45m];
    public string[] RsiOperators { get; init; } = ["lt", "gt", "cross_above", "cross_below"];

    // --- MACD ---
    public int[] MacdFastPeriods { get; init; } = [8, 12, 16];
    public int[] MacdSlowPeriods { get; init; } = [21, 26, 30];
    public int[] MacdSignalPeriods { get; init; } = [9];
    public string[] MacdOperators { get; init; } = ["cross_above_signal", "cross_below_signal", "above_zero", "histogram_rising"];

    // --- PriceVsEma ---
    public int[] EmaPeriods { get; init; } = [20, 50, 100];
    public decimal[] EmaProximityPercents { get; init; } = [0.15m, 0.25m, 0.5m];
    public string[] PriceVsEmaOperators { get; init; } = ["near", "above", "below", "cross_above"];

    // --- Exit ---
    public bool[] ExitOnOppositeSignalOptions { get; init; } = [false, true];

    // --- Risk ---
    public int[] MaxOpenTradesOptions { get; init; } = [1, 2, 3];
    public int[] CooldownCandlesOptions { get; init; } = [0, 1, 2, 3];

    // --- Trend Filter ---
    public bool IncludeTrendFilter { get; init; } = true;
    public int[][] TrendFilterPairs { get; init; } = [[20, 50], [50, 200]];
    public TrendFilterType[] TrendFilterTypes { get; init; } = [TrendFilterType.EmaCross, TrendFilterType.PriceAboveEma];
    public TrendOperator[] TrendFilterOperators { get; init; } = [TrendOperator.Above, TrendOperator.Below];
}