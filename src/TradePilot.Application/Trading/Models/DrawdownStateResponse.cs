namespace TradePilot.Application.Trading.Models;

public sealed record DrawdownStateResponse
{
    public required decimal DrawdownPercent { get; init; }

    public required decimal HighWaterMark { get; init; }

    public required decimal ScalingFactor { get; init; }

    public required bool IsCircuitBreakerActive { get; init; }

    public static DrawdownStateResponse Empty() => new()
    {
        DrawdownPercent = 0m,
        HighWaterMark = 0m,
        ScalingFactor = 1m,
        IsCircuitBreakerActive = false,
    };
}