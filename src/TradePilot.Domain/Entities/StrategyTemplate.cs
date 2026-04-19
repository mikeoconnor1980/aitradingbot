namespace TradePilot.Domain.Entities;

public sealed class StrategyTemplate
{
    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string StrategyMode { get; private set; } = string.Empty;
    public string Direction { get; private set; } = string.Empty;
    public string Market { get; private set; } = string.Empty;
    public string TagsJson { get; private set; } = "[]";
    public string ConfigJson { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsSystemTemplate { get; private set; }
    public bool IsBeginnerVisible { get; private set; }
    public bool IsActive { get; private set; }
    public long CreatedAtUtc { get; private set; }
    public long UpdatedAtUtc { get; private set; }

    private StrategyTemplate()
    {
    }

    public static StrategyTemplate Create(
        string slug,
        string name,
        string description,
        string strategyMode,
        string direction,
        string market,
        string tagsJson,
        string configJson,
        int sortOrder,
        bool isSystemTemplate = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(configJson);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new StrategyTemplate
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = name,
            Description = description,
            StrategyMode = strategyMode,
            Direction = direction,
            Market = market,
            TagsJson = tagsJson,
            ConfigJson = configJson,
            SortOrder = sortOrder,
            IsSystemTemplate = isSystemTemplate,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Update(
        string name,
        string description,
        string strategyMode,
        string direction,
        string market,
        string tagsJson,
        string configJson,
        int sortOrder,
        bool isSystemTemplate)
    {
        Name = name;
        Description = description;
        StrategyMode = strategyMode;
        Direction = direction;
        Market = market;
        TagsJson = tagsJson;
        ConfigJson = configJson;
        SortOrder = sortOrder;
        IsSystemTemplate = isSystemTemplate;
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void SetBeginnerVisibility(bool isBeginnerVisible)
    {
        IsBeginnerVisible = isBeginnerVisible;
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
