using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Entities;

/// <summary>
/// Durable factual evidence describing why a strategy did or did not produce an executable decision.
/// </summary>
public sealed class StrategyEvaluation
{
    private readonly List<RuleEvaluation> _rules = [];

    public Guid Id { get; private set; }
    public Guid? StrategyId { get; private set; }
    public string StrategyName { get; private set; } = string.Empty;
    public int? StrategyVersion { get; private set; }
    public string ConfigurationIdentity { get; private set; } = string.Empty;
    public string Symbol { get; private set; } = string.Empty;
    public string Timeframe { get; private set; } = string.Empty;
    public long EvaluatedAtUtc { get; private set; }
    public StrategyDecision Decision { get; private set; }
    public bool SetupDetected { get; private set; }
    public string? PrimaryRejectionReason { get; private set; }
    public long MarketContextTimestampUtc { get; private set; }
    public decimal ReferencePrice { get; private set; }
    public string? MarketRegime { get; private set; }
    public string? SignalType { get; private set; }
    public string? SignalReason { get; private set; }
    public bool EvaluationShortCircuited { get; private set; }
    public IReadOnlyList<RuleEvaluation> Rules => _rules;

    private StrategyEvaluation()
    {
    }

    /// <summary>Creates one complete evaluation record from evidence captured by the live strategy path.</summary>
    public static StrategyEvaluation Create(
        Guid? strategyId,
        string strategyName,
        int? strategyVersion,
        string configurationIdentity,
        string symbol,
        string timeframe,
        long evaluatedAtUtc,
        StrategyDecision decision,
        bool setupDetected,
        string? primaryRejectionReason,
        long marketContextTimestampUtc,
        decimal referencePrice,
        string? marketRegime,
        string? signalType,
        string? signalReason,
        bool evaluationShortCircuited,
        IEnumerable<RuleEvaluation> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeframe);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(evaluatedAtUtc);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(marketContextTimestampUtc);
        ArgumentOutOfRangeException.ThrowIfNegative(referencePrice);
        ArgumentNullException.ThrowIfNull(rules);

        var evaluation = new StrategyEvaluation
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            StrategyName = strategyName.Trim(),
            StrategyVersion = strategyVersion,
            ConfigurationIdentity = configurationIdentity.Trim(),
            Symbol = symbol.Trim().ToUpperInvariant(),
            Timeframe = timeframe.Trim(),
            EvaluatedAtUtc = evaluatedAtUtc,
            Decision = decision,
            SetupDetected = setupDetected,
            PrimaryRejectionReason = primaryRejectionReason,
            MarketContextTimestampUtc = marketContextTimestampUtc,
            ReferencePrice = referencePrice,
            MarketRegime = marketRegime,
            SignalType = signalType,
            SignalReason = signalReason,
            EvaluationShortCircuited = evaluationShortCircuited,
        };

        evaluation._rules.AddRange(rules.OrderBy(rule => rule.EvaluationOrder));
        return evaluation;
    }
}
