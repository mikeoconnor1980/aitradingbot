using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Entities;

public sealed class LlmContextSnapshot
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public string MarketSentiment { get; private set; } = string.Empty;
    public string MacroRegime { get; private set; } = string.Empty;
    public string EventRisk { get; private set; } = string.Empty;
    public decimal Confidence { get; private set; }
    public string DerivedRegime { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public long GeneratedAtUtc { get; private set; }

    private LlmContextSnapshot()
    {
    }

    public static LlmContextSnapshot Create(
        string symbol,
        string marketSentiment,
        string macroRegime,
        string eventRisk,
        decimal confidence,
        string derivedRegime,
        string summary,
        long generatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(marketSentiment);
        ArgumentException.ThrowIfNullOrWhiteSpace(macroRegime);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventRisk);
        ArgumentException.ThrowIfNullOrWhiteSpace(derivedRegime);

        return new LlmContextSnapshot
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            MarketSentiment = marketSentiment,
            MacroRegime = macroRegime,
            EventRisk = eventRisk,
            Confidence = confidence,
            DerivedRegime = derivedRegime,
            Summary = summary,
            GeneratedAtUtc = generatedAtUtc,
        };
    }
}
