namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed class TrendFilterResult
{
    public required bool Passed { get; init; }

    public required string Reason { get; init; }

    public string? ActualValue { get; init; }

    public decimal? ActualNumericValue { get; init; }

    public string? ExpectedValue { get; init; }

    public decimal? ExpectedNumericValue { get; init; }

    public static TrendFilterResult Pass(string reason) => new()
    {
        Passed = true,
        Reason = reason,
    };

    public static TrendFilterResult Fail(string reason) => new()
    {
        Passed = false,
        Reason = reason,
    };
}
