namespace TradingApp.Application.Backtesting.Models;

public sealed class BacktestConfig
{
    public required string Symbol { get; init; }
    public required IReadOnlyList<string> Intervals { get; init; }
    public required long StartDateUtc { get; init; }
    public required long EndDateUtc { get; init; }
    public required decimal InitialCapital { get; init; }
    public required FeeModel FeeModel { get; init; }
    public int WarmupPeriod { get; init; } = 200;
    public required string StrategyConfigJson { get; init; }
    public bool EnableAuditLog { get; init; } = true;
}
