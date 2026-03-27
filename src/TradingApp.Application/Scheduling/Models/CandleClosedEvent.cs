using TradingApp.Domain.Entities;

namespace TradingApp.Application.Scheduling.Models;

public sealed class CandleClosedEvent
{
    public required string Symbol { get; init; }
    public required string Timeframe { get; init; }
    public required long OpenTimeUtc { get; init; }
    public required long CloseTimeUtc { get; init; }
    public required Candle Candle { get; init; }
}
