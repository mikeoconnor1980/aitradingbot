namespace TradePilot.Domain.Entities;

public sealed class StrategyReview
{
    public Guid Id { get; private set; }
    public Guid StrategyId { get; private set; }
    public int RevisionNumber { get; private set; }
    public string ReviewMarkdown { get; private set; } = string.Empty;
    public string ModelName { get; private set; } = string.Empty;
    public bool IsFallback { get; private set; }
    public long CreatedAtUtc { get; private set; }

    private StrategyReview()
    {
    }

    public static StrategyReview Create(
        Guid strategyId,
        int revisionNumber,
        string reviewMarkdown,
        string modelName,
        bool isFallback = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(revisionNumber, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewMarkdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);

        if (strategyId == Guid.Empty)
        {
            throw new ArgumentException("Strategy ID must not be empty.", nameof(strategyId));
        }

        return new StrategyReview
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            RevisionNumber = revisionNumber,
            ReviewMarkdown = reviewMarkdown,
            ModelName = modelName,
            IsFallback = isFallback,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }
}