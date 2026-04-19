namespace TradePilot.Domain.Entities;

public sealed class Strategy
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string StrategyType { get; private set; } = string.Empty;
    public string ConfigJson { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsRunning { get; private set; }
    public string? AssignedAgentId { get; private set; }
    public long? LastStartedAtUtc { get; private set; }
    public long? LastStoppedAtUtc { get; private set; }
    public decimal? HighWaterMarkUsd { get; private set; }
    public long CreatedAtUtc { get; private set; }
    public long UpdatedAtUtc { get; private set; }

    private Strategy()
    {
    }

    public static Strategy Create(
        string userId,
        string name,
        string strategyType,
        string configJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(configJson);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new Strategy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            StrategyType = strategyType,
            ConfigJson = configJson,
            Version = 1,
            IsActive = true,
            IsRunning = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Update(string name, string configJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(configJson);

        Name = name;
        ConfigJson = configJson;
        Version++;
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void SoftDelete()
    {
        IsActive = false;
        IsRunning = false;
        AssignedAgentId = null;
        LastStoppedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void SetRunningState(bool isRunning)
    {
        if (isRunning && !IsActive)
        {
            throw new InvalidOperationException("Cannot start an inactive strategy.");
        }

        IsRunning = isRunning;
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (!isRunning)
        {
            AssignedAgentId = null;
            LastStoppedAtUtc = UpdatedAtUtc;
        }
    }

    public void AssignToAgentAndStart(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        if (!IsActive)
        {
            throw new InvalidOperationException("Cannot start an inactive strategy.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        IsRunning = true;
        AssignedAgentId = agentId;
        LastStartedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void StopLiveTrading()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        IsRunning = false;
        AssignedAgentId = null;
        LastStoppedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void UpdateHighWaterMark(decimal highWaterMark)
    {
        HighWaterMarkUsd = highWaterMark;
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}