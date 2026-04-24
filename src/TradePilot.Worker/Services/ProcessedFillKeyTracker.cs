namespace TradePilot.Worker.Services;

internal sealed class ProcessedFillKeyTracker
{
    internal static readonly TimeSpan FillKeyRetention = TimeSpan.FromHours(2);
    internal static readonly TimeSpan FillKeyCompactionInterval = TimeSpan.FromMinutes(30);

    private readonly Dictionary<string, DateTimeOffset> _processedFillKeys;
    private DateTimeOffset _lastCompactionUtc;

    public ProcessedFillKeyTracker(DateTimeOffset? initialUtcNow = null)
    {
        _processedFillKeys = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        _lastCompactionUtc = initialUtcNow ?? DateTimeOffset.UtcNow;
    }

    internal int Count => _processedFillKeys.Count;

    internal bool Contains(string fillKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillKey);
        return _processedFillKeys.ContainsKey(fillKey);
    }

    public bool TryRegister(string fillKey, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fillKey);

        Compact(utcNow);

        if (_processedFillKeys.ContainsKey(fillKey))
        {
            return false;
        }

        _processedFillKeys[fillKey] = utcNow;
        return true;
    }

    internal void Compact(DateTimeOffset utcNow)
    {
        if (utcNow - _lastCompactionUtc < FillKeyCompactionInterval)
        {
            return;
        }

        var cutoff = utcNow - FillKeyRetention;
        var expiredKeys = _processedFillKeys
            .Where(entry => entry.Value < cutoff)
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var expiredKey in expiredKeys)
        {
            _processedFillKeys.Remove(expiredKey);
        }

        _lastCompactionUtc = utcNow;
    }
}