namespace TradingApp.Application.Trading.Models;

/// <summary>
/// A trading signal emitted by the grid controller.
/// This will be expanded to typed signal contracts in future work.
/// </summary>
public sealed class TradingSignal
{
    public required string SignalType { get; init; }
    public required string Symbol { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyDictionary<string, object>? Parameters { get; init; }
}
