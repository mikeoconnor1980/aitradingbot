namespace TradingApp.Domain.Entities;

public sealed class MacroSyncRun
{
    public Guid Id { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public long StartedAtUtc { get; private set; }
    public long? CompletedAtUtc { get; private set; }
    public bool Succeeded { get; private set; }
    public int EventsFetched { get; private set; }
    public int EventsInserted { get; private set; }
    public int EventsUpdated { get; private set; }
    public string? Error { get; private set; }

    private MacroSyncRun()
    {
    }

    public static MacroSyncRun Start(string provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        return new MacroSyncRun
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            StartedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Succeeded = false,
        };
    }

    public void Complete(int fetched, int inserted, int updated)
    {
        CompletedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Succeeded = true;
        EventsFetched = fetched;
        EventsInserted = inserted;
        EventsUpdated = updated;
    }

    public void Fail(string error)
    {
        CompletedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Succeeded = false;
        Error = error;
    }
}
