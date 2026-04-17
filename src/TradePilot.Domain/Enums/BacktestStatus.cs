namespace TradePilot.Domain.Enums;

public enum BacktestStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
}
