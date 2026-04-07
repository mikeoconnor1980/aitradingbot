namespace TradingApp.Application.LlmContextSnapshots.Models;

public sealed class LlmContextDto
{
    public required string Symbol { get; init; }
    public required string MarketSentiment { get; init; }
    public required string MacroRegime { get; init; }
    public required string EventRisk { get; init; }
    public required decimal Confidence { get; init; }
    public required string DerivedRegime { get; init; }
    public string Summary { get; init; } = string.Empty;
    public required long GeneratedAtUtc { get; init; }
}
