using TradingApp.Domain.Entities;

namespace TradingApp.Application.Backtesting.Models;

public sealed class ReplayData
{
    public required IReadOnlyList<Candle> Candles15m { get; init; }
    public required IReadOnlyList<Candle> Candles1h { get; init; }
    public required IReadOnlyList<Candle> Candles4h { get; init; }
    public required IReadOnlyList<Candle> TriggerCandles { get; init; }
    public required string TriggerTimeframe { get; init; }
    public required int WarmupEndIndex { get; init; }
}