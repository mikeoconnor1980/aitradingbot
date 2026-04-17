using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Entities;

public sealed class OptimizationRun
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public long StartDateUtc { get; private set; }
    public long EndDateUtc { get; private set; }
    public decimal InitialCapital { get; private set; }
    public string SweepConfigJson { get; private set; } = string.Empty;
    public string ThresholdsJson { get; private set; } = string.Empty;
    public int TotalCombinations { get; private set; }
    public int CompletedCount { get; private set; }
    public int QualifiedCount { get; private set; }
    public int FailedCount { get; private set; }
    public OptimizationStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public long ElapsedMs { get; private set; }
    public long CreatedAtUtc { get; private set; }

    private OptimizationRun()
    {
    }

    public static OptimizationRun CreateQueued(
        string symbol,
        long startDateUtc,
        long endDateUtc,
        decimal initialCapital,
        string sweepConfigJson,
        string thresholdsJson,
        int totalCombinations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(sweepConfigJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(thresholdsJson);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapital);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startDateUtc, endDateUtc);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalCombinations);

        return new OptimizationRun
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            InitialCapital = initialCapital,
            SweepConfigJson = sweepConfigJson,
            ThresholdsJson = thresholdsJson,
            TotalCombinations = totalCombinations,
            CompletedCount = 0,
            QualifiedCount = 0,
            Status = OptimizationStatus.Queued,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }

    public void MarkRunning()
    {
        Status = OptimizationStatus.Running;
        ErrorMessage = null;
    }

    public void UpdateProgress(int completed, int total)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(completed);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(total);

        if (completed > total)
        {
            throw new ArgumentOutOfRangeException(nameof(completed), "Completed count cannot exceed total.");
        }

        CompletedCount = completed;
        TotalCombinations = total;
    }

    public void MarkCompleted(int qualifiedCount, int failedCount, long elapsedMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(qualifiedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(failedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedMs);

        Status = OptimizationStatus.Completed;
        CompletedCount = TotalCombinations;
        QualifiedCount = qualifiedCount;
        FailedCount = failedCount;
        ElapsedMs = elapsedMs;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        Status = OptimizationStatus.Failed;
        ErrorMessage = errorMessage;
    }

    public void MarkCancelled()
    {
        Status = OptimizationStatus.Cancelled;
        ErrorMessage = null;
    }
}