using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Entities;

public sealed class StrategyRevision
{
    public Guid Id { get; private set; }
    public Guid StrategyId { get; private set; }
    public int RevisionNumber { get; private set; }
    public string ConfigJson { get; private set; } = string.Empty;
    public RevisionSource Source { get; private set; }
    public string? Label { get; private set; }
    public string ChangeSummary { get; private set; } = string.Empty;
    public long CreatedAtUtc { get; private set; }

    private StrategyRevision()
    {
    }

    public static StrategyRevision Create(
        Guid strategyId,
        int revisionNumber,
        string configJson,
        RevisionSource source,
        string changeSummary,
        string? label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(changeSummary);
        ArgumentOutOfRangeException.ThrowIfLessThan(revisionNumber, 1);

        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Invalid revision source.");
        }

        if (strategyId == Guid.Empty)
        {
            throw new ArgumentException("Strategy ID must not be empty.", nameof(strategyId));
        }

        return new StrategyRevision
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            RevisionNumber = revisionNumber,
            ConfigJson = configJson,
            Source = source,
            Label = label,
            ChangeSummary = changeSummary,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }
}