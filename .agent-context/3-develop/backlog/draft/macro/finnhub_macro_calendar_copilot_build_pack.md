# Finnhub Macro Calendar Copilot Build Pack

## Purpose

This build pack is designed to help generate the first working version of a **macro economic calendar subsystem** for your trading app using:

- **.NET 8**
- **ASP.NET Core Web API**
- **Entity Framework Core**
- **SQLite** for local/dev or small deployments
- **BackgroundService** for sync jobs
- **Angular** for a simple calendar list UI
- **Risk Engine integration** so macro events can block **new entries** but still allow **exits / sells / reduce-only actions**

This pack is intentionally opinionated and practical. It is aimed at getting a solid MVP built quickly while keeping the architecture clean enough to swap providers later.

---

# 1. Scope

## In scope

- Pull macro calendar events from a provider abstraction
- Implement a **Finnhub provider**
- Normalize events into your own domain model
- Store events in your database
- Run scheduled background sync
- Expose API endpoints for querying upcoming events and active block windows
- Build Angular UI to show a calendar list
- Integrate macro event windows into your risk engine

## Out of scope for MVP

- Multiple providers in production at once
- Webhooks / streaming providers
- Historical analytics on event impact
- Forecast surprise scoring
- News sentiment enrichment
- Full calendar month grid UI
- User-custom event watchlists

---

# 2. Architecture Overview

```text
Finnhub API
   ->
FinnhubMacroCalendarProvider
   ->
MacroCalendarIngestionService
   ->
MacroEventNormalizer
   ->
Database (MacroEvents, MacroBlockWindows)
   ->
ASP.NET API
   ->
Angular Calendar List
   ->
Risk Engine / Execution Gate
```

## Key design principles

1. **Never let the rest of the app depend directly on Finnhub response models**
2. **Always normalize provider data into your own domain entities**
3. **Store raw source payload where helpful for debugging**
4. **Treat macro blocking as a risk rule on entries only**
5. **Always allow exits, stop losses, take profits, and reduce-only actions**
6. **Design for provider swap later**

---

# 3. Recommended Project Structure

```text
src/
  TradingApp.Api/
    Controllers/
    Program.cs

  TradingApp.Application/
    Interfaces/
    Services/
    Models/
    Risk/

  TradingApp.Domain/
    Entities/
    Enums/
    ValueObjects/

  TradingApp.Infrastructure/
    Data/
    Providers/
      MacroCalendar/
        Finnhub/
    BackgroundJobs/
    Configuration/

  TradingApp.Angular/
    src/app/features/macro-calendar/
```

---

# 4. Domain Model

## 4.1 MacroEvent entity

```csharp
public class MacroEvent
{
    public Guid Id { get; set; }

    public string Provider { get; set; } = default!;
    public string ProviderEventId { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string Currency { get; set; } = default!;
    public string Category { get; set; } = default!;

    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }

    public MacroEventImportance Importance { get; set; }
    public MacroEventStatus Status { get; set; }

    public string? Actual { get; set; }
    public string? Forecast { get; set; }
    public string? Previous { get; set; }
    public string? Revised { get; set; }

    public int DefaultPreBlockMinutes { get; set; }
    public int DefaultPostBlockMinutes { get; set; }

    public DateTime BlockStartUtc { get; set; }
    public DateTime BlockEndUtc { get; set; }

    public string? SourceUrl { get; set; }
    public string? RawPayloadJson { get; set; }

    public DateTime LastSeenUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
```

## 4.2 Enums

```csharp
public enum MacroEventImportance
{
    Unknown = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum MacroEventStatus
{
    Scheduled = 0,
    Live = 1,
    Released = 2,
    Revised = 3,
    Cancelled = 4
}
```

## 4.3 Optional separate block window entity

You can compute block windows directly on `MacroEvent`, but a separate entity is cleaner if you later support:

- per-strategy overrides
- custom user policies
- provider-independent rule history

```csharp
public class MacroBlockWindow
{
    public Guid Id { get; set; }
    public Guid MacroEventId { get; set; }

    public DateTime BlockStartUtc { get; set; }
    public DateTime BlockEndUtc { get; set; }

    public bool BlocksNewEntries { get; set; }
    public bool AllowsExits { get; set; }
    public bool AllowsReduceOnly { get; set; }

    public string PolicyName { get; set; } = default!;
    public bool IsActive { get; set; }

    public MacroEvent MacroEvent { get; set; } = default!;
}
```

---

# 5. Database Design

## 5.1 EF Core configuration

```csharp
public class MacroEventConfiguration : IEntityTypeConfiguration<MacroEvent>
{
    public void Configure(EntityTypeBuilder<MacroEvent> builder)
    {
        builder.ToTable("MacroEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ProviderEventId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Country).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => new { x.Provider, x.ProviderEventId }).IsUnique();
        builder.HasIndex(x => x.ScheduledAtUtc);
        builder.HasIndex(x => x.BlockStartUtc);
        builder.HasIndex(x => x.BlockEndUtc);
        builder.HasIndex(x => x.Importance);
        builder.HasIndex(x => x.Status);
    }
}
```

## 5.2 Suggested tables

- `MacroEvents`
- `MacroBlockWindows` (optional for MVP)
- `MacroSyncRuns`
- `MacroProviderHealthChecks` (optional)

## 5.3 Sync run tracking

```csharp
public class MacroSyncRun
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = default!;
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public bool Succeeded { get; set; }
    public int EventsFetched { get; set; }
    public int EventsInserted { get; set; }
    public int EventsUpdated { get; set; }
    public string? Error { get; set; }
}
```

---

# 6. Configuration

## 6.1 App settings

```json
{
  "MacroCalendar": {
    "Provider": "Finnhub",
    "Enabled": true,
    "LookAheadDays": 7,
    "LookBackDays": 1,
    "NearEventWindowMinutes": 10,
    "FullSyncIntervalMinutes": 360,
    "IncrementalSyncIntervalMinutes": 5,
    "NearEventSyncIntervalSeconds": 60,
    "DefaultPolicies": {
      "High": {
        "PreBlockMinutes": 30,
        "PostBlockMinutes": 30
      },
      "Medium": {
        "PreBlockMinutes": 10,
        "PostBlockMinutes": 10
      },
      "Low": {
        "PreBlockMinutes": 0,
        "PostBlockMinutes": 0
      }
    }
  },
  "Finnhub": {
    "ApiKey": "YOUR_API_KEY",
    "BaseUrl": "https://finnhub.io/api/v1"
  }
}
```

## 6.2 Options classes

```csharp
public class MacroCalendarOptions
{
    public string Provider { get; set; } = "Finnhub";
    public bool Enabled { get; set; }
    public int LookAheadDays { get; set; } = 7;
    public int LookBackDays { get; set; } = 1;
    public int NearEventWindowMinutes { get; set; } = 10;
    public int FullSyncIntervalMinutes { get; set; } = 360;
    public int IncrementalSyncIntervalMinutes { get; set; } = 5;
    public int NearEventSyncIntervalSeconds { get; set; } = 60;
    public MacroPolicyOptions DefaultPolicies { get; set; } = new();
}

public class MacroPolicyOptions
{
    public MacroBlockPolicy High { get; set; } = new();
    public MacroBlockPolicy Medium { get; set; } = new();
    public MacroBlockPolicy Low { get; set; } = new();
}

public class MacroBlockPolicy
{
    public int PreBlockMinutes { get; set; }
    public int PostBlockMinutes { get; set; }
}
```

---

# 7. Provider Abstraction

## 7.1 Provider contract

```csharp
public interface IMacroCalendarProvider
{
    string ProviderName { get; }

    Task<IReadOnlyCollection<ExternalMacroEventDto>> GetEventsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);
}
```

## 7.2 External DTO

Keep this generic enough for any provider.

```csharp
public class ExternalMacroEventDto
{
    public string ProviderEventId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string Currency { get; set; } = default!;
    public string Category { get; set; } = default!;
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public string ImportanceRaw { get; set; } = default!;
    public string StatusRaw { get; set; } = default!;
    public string? Actual { get; set; }
    public string? Forecast { get; set; }
    public string? Previous { get; set; }
    public string? Revised { get; set; }
    public string? SourceUrl { get; set; }
    public string RawPayloadJson { get; set; } = default!;
}
```

---

# 8. Finnhub Implementation

## 8.1 Finnhub HTTP client registration

```csharp
services.AddHttpClient<IFinnhubClient, FinnhubClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<FinnhubOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

## 8.2 Finnhub options

```csharp
public class FinnhubOptions
{
    public string ApiKey { get; set; } = default!;
    public string BaseUrl { get; set; } = default!;
}
```

## 8.3 Finnhub client contract

```csharp
public interface IFinnhubClient
{
    Task<IReadOnlyCollection<FinnhubEconomicCalendarItem>> GetEconomicCalendarAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);
}
```

## 8.4 Finnhub response model

Keep this provider-specific and isolated in infrastructure.

```csharp
public class FinnhubEconomicCalendarItem
{
    public string? Country { get; set; }
    public string? Event { get; set; }
    public string? Impact { get; set; }
    public string? Unit { get; set; }
    public string? Actual { get; set; }
    public string? Forecast { get; set; }
    public string? Prev { get; set; }
    public long? Time { get; set; }
}
```

## 8.5 Finnhub HTTP client example

```csharp
public class FinnhubClient : IFinnhubClient
{
    private readonly HttpClient _httpClient;
    private readonly FinnhubOptions _options;

    public FinnhubClient(HttpClient httpClient, IOptions<FinnhubOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyCollection<FinnhubEconomicCalendarItem>> GetEconomicCalendarAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var from = fromUtc.ToString("yyyy-MM-dd");
        var to = toUtc.ToString("yyyy-MM-dd");

        var url = $"calendar/economic?from={from}&to={to}&token={_options.ApiKey}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        var items = JsonSerializer.Deserialize<List<FinnhubEconomicCalendarItem>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return items ?? [];
    }
}
```

## 8.6 Provider adapter

```csharp
public class FinnhubMacroCalendarProvider : IMacroCalendarProvider
{
    private readonly IFinnhubClient _client;

    public string ProviderName => "Finnhub";

    public FinnhubMacroCalendarProvider(IFinnhubClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyCollection<ExternalMacroEventDto>> GetEventsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var items = await _client.GetEconomicCalendarAsync(fromUtc, toUtc, cancellationToken);

        return items
            .Where(x => !string.IsNullOrWhiteSpace(x.Event) && x.Time.HasValue)
            .Select(x => new ExternalMacroEventDto
            {
                ProviderEventId = BuildProviderEventId(x),
                Title = x.Event ?? "Unknown Event",
                Country = x.Country ?? "Unknown",
                Currency = InferCurrency(x.Country),
                Category = InferCategory(x.Event),
                ScheduledAtUtc = DateTimeOffset.FromUnixTimeSeconds(x.Time!.Value).UtcDateTime,
                ImportanceRaw = x.Impact ?? "Unknown",
                StatusRaw = "Scheduled",
                Actual = x.Actual,
                Forecast = x.Forecast,
                Previous = x.Prev,
                RawPayloadJson = JsonSerializer.Serialize(x)
            })
            .ToList();
    }

    private static string BuildProviderEventId(FinnhubEconomicCalendarItem item)
    {
        return $"{item.Country}|{item.Event}|{item.Time}";
    }

    private static string InferCurrency(string? country)
    {
        return country switch
        {
            "United States" => "USD",
            "Euro Area" => "EUR",
            "United Kingdom" => "GBP",
            "Japan" => "JPY",
            _ => "UNK"
        };
    }

    private static string InferCategory(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "general";

        var value = title.ToLowerInvariant();

        if (value.Contains("cpi") || value.Contains("inflation"))
            return "inflation";
        if (value.Contains("rate") || value.Contains("fomc") || value.Contains("central bank"))
            return "rates";
        if (value.Contains("payroll") || value.Contains("employment") || value.Contains("jobless"))
            return "labour";
        if (value.Contains("gdp"))
            return "growth";

        return "general";
    }
}
```

---

# 9. Normalization Rules

## 9.1 Importance mapping

```csharp
public static class MacroImportanceMapper
{
    public static MacroEventImportance Map(string raw)
    {
        var value = raw?.Trim().ToLowerInvariant() ?? string.Empty;

        return value switch
        {
            "high" => MacroEventImportance.High,
            "medium" => MacroEventImportance.Medium,
            "low" => MacroEventImportance.Low,
            "critical" => MacroEventImportance.Critical,
            _ => MacroEventImportance.Unknown
        };
    }
}
```

## 9.2 Status mapping

```csharp
public static class MacroStatusMapper
{
    public static MacroEventStatus Map(string raw)
    {
        var value = raw?.Trim().ToLowerInvariant() ?? string.Empty;

        return value switch
        {
            "live" => MacroEventStatus.Live,
            "released" => MacroEventStatus.Released,
            "revised" => MacroEventStatus.Revised,
            "cancelled" => MacroEventStatus.Cancelled,
            _ => MacroEventStatus.Scheduled
        };
    }
}
```

## 9.3 Block window policy

```csharp
public interface IMacroBlockWindowCalculator
{
    (int preMinutes, int postMinutes) GetWindow(MacroEventImportance importance, string category);
}
```

```csharp
public class MacroBlockWindowCalculator : IMacroBlockWindowCalculator
{
    private readonly MacroCalendarOptions _options;

    public MacroBlockWindowCalculator(IOptions<MacroCalendarOptions> options)
    {
        _options = options.Value;
    }

    public (int preMinutes, int postMinutes) GetWindow(MacroEventImportance importance, string category)
    {
        return importance switch
        {
            MacroEventImportance.High => (
                _options.DefaultPolicies.High.PreBlockMinutes,
                _options.DefaultPolicies.High.PostBlockMinutes),

            MacroEventImportance.Medium => (
                _options.DefaultPolicies.Medium.PreBlockMinutes,
                _options.DefaultPolicies.Medium.PostBlockMinutes),

            MacroEventImportance.Low => (
                _options.DefaultPolicies.Low.PreBlockMinutes,
                _options.DefaultPolicies.Low.PostBlockMinutes),

            _ => (0, 0)
        };
    }
}
```

---

# 10. Ingestion Service

## 10.1 Contract

```csharp
public interface IMacroCalendarIngestionService
{
    Task<MacroSyncResult> SyncAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}
```

## 10.2 Result model

```csharp
public class MacroSyncResult
{
    public int Fetched { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
}
```

## 10.3 Service implementation sketch

```csharp
public class MacroCalendarIngestionService : IMacroCalendarIngestionService
{
    private readonly IMacroCalendarProvider _provider;
    private readonly TradingAppDbContext _dbContext;
    private readonly IMacroBlockWindowCalculator _windowCalculator;
    private readonly ILogger<MacroCalendarIngestionService> _logger;

    public MacroCalendarIngestionService(
        IMacroCalendarProvider provider,
        TradingAppDbContext dbContext,
        IMacroBlockWindowCalculator windowCalculator,
        ILogger<MacroCalendarIngestionService> logger)
    {
        _provider = provider;
        _dbContext = dbContext;
        _windowCalculator = windowCalculator;
        _logger = logger;
    }

    public async Task<MacroSyncResult> SyncAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var externalEvents = await _provider.GetEventsAsync(fromUtc, toUtc, cancellationToken);

        var result = new MacroSyncResult
        {
            Fetched = externalEvents.Count
        };

        foreach (var external in externalEvents)
        {
            var importance = MacroImportanceMapper.Map(external.ImportanceRaw);
            var status = MacroStatusMapper.Map(external.StatusRaw);
            var (pre, post) = _windowCalculator.GetWindow(importance, external.Category);

            var existing = await _dbContext.MacroEvents
                .FirstOrDefaultAsync(x =>
                    x.Provider == _provider.ProviderName &&
                    x.ProviderEventId == external.ProviderEventId,
                    cancellationToken);

            if (existing is null)
            {
                var entity = new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    Provider = _provider.ProviderName,
                    ProviderEventId = external.ProviderEventId,
                    Title = external.Title,
                    Country = external.Country,
                    Currency = external.Currency,
                    Category = external.Category,
                    ScheduledAtUtc = external.ScheduledAtUtc,
                    ReleasedAtUtc = external.ReleasedAtUtc,
                    Importance = importance,
                    Status = status,
                    Actual = external.Actual,
                    Forecast = external.Forecast,
                    Previous = external.Previous,
                    Revised = external.Revised,
                    DefaultPreBlockMinutes = pre,
                    DefaultPostBlockMinutes = post,
                    BlockStartUtc = external.ScheduledAtUtc.AddMinutes(-pre),
                    BlockEndUtc = external.ScheduledAtUtc.AddMinutes(post),
                    SourceUrl = external.SourceUrl,
                    RawPayloadJson = external.RawPayloadJson,
                    LastSeenUtc = DateTime.UtcNow,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                };

                _dbContext.MacroEvents.Add(entity);
                result.Inserted++;
            }
            else
            {
                existing.Title = external.Title;
                existing.Country = external.Country;
                existing.Currency = external.Currency;
                existing.Category = external.Category;
                existing.ScheduledAtUtc = external.ScheduledAtUtc;
                existing.ReleasedAtUtc = external.ReleasedAtUtc;
                existing.Importance = importance;
                existing.Status = status;
                existing.Actual = external.Actual;
                existing.Forecast = external.Forecast;
                existing.Previous = external.Previous;
                existing.Revised = external.Revised;
                existing.DefaultPreBlockMinutes = pre;
                existing.DefaultPostBlockMinutes = post;
                existing.BlockStartUtc = external.ScheduledAtUtc.AddMinutes(-pre);
                existing.BlockEndUtc = external.ScheduledAtUtc.AddMinutes(post);
                existing.SourceUrl = external.SourceUrl;
                existing.RawPayloadJson = external.RawPayloadJson;
                existing.LastSeenUtc = DateTime.UtcNow;
                existing.UpdatedUtc = DateTime.UtcNow;

                result.Updated++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }
}
```

---

# 11. Background Worker

## 11.1 Strategy

Use a single hosted service with three modes:

1. **Full sync**
   - every 6 hours
   - pulls yesterday through next 7 days

2. **Incremental sync**
   - every 5 minutes
   - pulls now through next 24 hours

3. **Near-event sync**
   - every 60 seconds
   - if a high-impact event is within 10 minutes

For MVP you can simplify this to a single loop with dynamic timing.

## 11.2 Hosted service

```csharp
public class MacroCalendarSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<MacroCalendarOptions> _options;
    private readonly ILogger<MacroCalendarSyncWorker> _logger;

    public MacroCalendarSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<MacroCalendarOptions> options,
        ILogger<MacroCalendarSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            _logger.LogInformation("Macro calendar sync worker is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var ingestion = scope.ServiceProvider.GetRequiredService<IMacroCalendarIngestionService>();
                var db = scope.ServiceProvider.GetRequiredService<TradingAppDbContext>();

                var now = DateTime.UtcNow;
                var fullSyncNeeded = ShouldRunFullSync(now);

                if (fullSyncNeeded)
                {
                    var from = now.Date.AddDays(-_options.Value.LookBackDays);
                    var to = now.Date.AddDays(_options.Value.LookAheadDays);
                    await ingestion.SyncAsync(from, to, stoppingToken);

                    await Task.Delay(TimeSpan.FromMinutes(_options.Value.IncrementalSyncIntervalMinutes), stoppingToken);
                    continue;
                }

                var nearEventExists = await db.MacroEvents.AnyAsync(x =>
                    x.Importance >= MacroEventImportance.High &&
                    x.ScheduledAtUtc >= now &&
                    x.ScheduledAtUtc <= now.AddMinutes(_options.Value.NearEventWindowMinutes),
                    stoppingToken);

                if (nearEventExists)
                {
                    await ingestion.SyncAsync(now.AddHours(-1), now.AddHours(6), stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(_options.Value.NearEventSyncIntervalSeconds), stoppingToken);
                }
                else
                {
                    await ingestion.SyncAsync(now.AddHours(-6), now.AddDays(1), stoppingToken);
                    await Task.Delay(TimeSpan.FromMinutes(_options.Value.IncrementalSyncIntervalMinutes), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Macro calendar sync failed.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private static bool ShouldRunFullSync(DateTime nowUtc)
    {
        return nowUtc.Hour % 6 == 0 && nowUtc.Minute < 5;
    }
}
```

---

# 12. Query Service

## 12.1 Contract

```csharp
public interface IMacroCalendarQueryService
{
    Task<IReadOnlyCollection<MacroEventListItemDto>> GetUpcomingEventsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? currency,
        MacroEventImportance? minimumImportance,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MacroEventListItemDto>> GetActiveBlockWindowsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
```

## 12.2 DTO

```csharp
public class MacroEventListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string Currency { get; set; } = default!;
    public string Category { get; set; } = default!;
    public DateTime ScheduledAtUtc { get; set; }
    public MacroEventImportance Importance { get; set; }
    public MacroEventStatus Status { get; set; }
    public string? Forecast { get; set; }
    public string? Previous { get; set; }
    public string? Actual { get; set; }
    public DateTime BlockStartUtc { get; set; }
    public DateTime BlockEndUtc { get; set; }
    public bool IsBlockingNow { get; set; }
}
```

---

# 13. API Endpoints

## 13.1 Controller routes

```text
GET /api/macro-calendar/events
GET /api/macro-calendar/active-blocks
POST /api/macro-calendar/sync
```

## 13.2 Controller example

```csharp
[ApiController]
[Route("api/macro-calendar")]
public class MacroCalendarController : ControllerBase
{
    private readonly IMacroCalendarQueryService _queryService;
    private readonly IMacroCalendarIngestionService _ingestionService;

    public MacroCalendarController(
        IMacroCalendarQueryService queryService,
        IMacroCalendarIngestionService ingestionService)
    {
        _queryService = queryService;
        _ingestionService = ingestionService;
    }

    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyCollection<MacroEventListItemDto>>> GetEvents(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] string? currency,
        [FromQuery] MacroEventImportance? minimumImportance,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetUpcomingEventsAsync(
            fromUtc, toUtc, currency, minimumImportance, cancellationToken);

        return Ok(result);
    }

    [HttpGet("active-blocks")]
    public async Task<ActionResult<IReadOnlyCollection<MacroEventListItemDto>>> GetActiveBlocks(
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetActiveBlockWindowsAsync(DateTime.UtcNow, cancellationToken);
        return Ok(result);
    }

    [HttpPost("sync")]
    public async Task<ActionResult<MacroSyncResult>> Sync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var result = await _ingestionService.SyncAsync(now.AddDays(-1), now.AddDays(7), cancellationToken);
        return Ok(result);
    }
}
```

---

# 14. Risk Engine Integration

This is the important behaviour:

- **Block new entries**
- **Allow exits**
- **Allow take profits**
- **Allow stop losses**
- **Allow reduce-only sells**
- **Optionally allow hedging only if explicitly enabled**

## 14.1 Order intent model

```csharp
public enum TradeIntent
{
    NewEntry = 0,
    Exit = 1,
    ReduceOnly = 2,
    StopLoss = 3,
    TakeProfit = 4
}
```

## 14.2 Risk rule contract

```csharp
public interface IRiskRule
{
    Task<RiskCheckResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken);
}
```

## 14.3 Macro risk rule

```csharp
public class MacroEventRiskRule : IRiskRule
{
    private readonly TradingAppDbContext _dbContext;

    public MacroEventRiskRule(TradingAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RiskCheckResult> EvaluateAsync(RiskContext context, CancellationToken cancellationToken)
    {
        if (context.TradeIntent is TradeIntent.Exit or TradeIntent.ReduceOnly or TradeIntent.StopLoss or TradeIntent.TakeProfit)
        {
            return RiskCheckResult.Allow("Exits and reductions are allowed during macro windows.");
        }

        var now = DateTime.UtcNow;

        var activeBlock = await _dbContext.MacroEvents
            .Where(x =>
                x.BlockStartUtc <= now &&
                x.BlockEndUtc >= now &&
                x.Importance >= MacroEventImportance.High)
            .OrderBy(x => x.ScheduledAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeBlock is null)
        {
            return RiskCheckResult.Allow("No active macro block window.");
        }

        return RiskCheckResult.Deny(
            $"New entries blocked due to macro event: {activeBlock.Title} ({activeBlock.Country})");
    }
}
```

## 14.4 Risk result model

```csharp
public class RiskCheckResult
{
    public bool IsAllowed { get; private set; }
    public string Reason { get; private set; } = default!;

    public static RiskCheckResult Allow(string reason) => new()
    {
        IsAllowed = true,
        Reason = reason
    };

    public static RiskCheckResult Deny(string reason) => new()
    {
        IsAllowed = false,
        Reason = reason
    };
}
```

---

# 15. Angular Build

## 15.1 Feature structure

```text
src/app/features/macro-calendar/
  macro-calendar-page.component.ts
  macro-calendar-page.component.html
  macro-calendar-page.component.scss
  macro-calendar.service.ts
  models/
    macro-event-list-item.ts
```

## 15.2 Angular model

```ts
export interface MacroEventListItem {
  id: string;
  title: string;
  country: string;
  currency: string;
  category: string;
  scheduledAtUtc: string;
  importance: number;
  status: number;
  forecast?: string | null;
  previous?: string | null;
  actual?: string | null;
  blockStartUtc: string;
  blockEndUtc: string;
  isBlockingNow: boolean;
}
```

## 15.3 Angular service

```ts
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { MacroEventListItem } from './models/macro-event-list-item';

@Injectable({ providedIn: 'root' })
export class MacroCalendarService {
  constructor(private http: HttpClient) {}

  getUpcomingEvents(fromUtc: string, toUtc: string, currency?: string): Observable<MacroEventListItem[]> {
    let params = new HttpParams()
      .set('fromUtc', fromUtc)
      .set('toUtc', toUtc);

    if (currency) {
      params = params.set('currency', currency);
    }

    return this.http.get<MacroEventListItem[]>('/api/macro-calendar/events', { params });
  }

  getActiveBlocks(): Observable<MacroEventListItem[]> {
    return this.http.get<MacroEventListItem[]>('/api/macro-calendar/active-blocks');
  }
}
```

## 15.4 Component example

```ts
import { Component, OnInit } from '@angular/core';
import { MacroCalendarService } from './macro-calendar.service';
import { MacroEventListItem } from './models/macro-event-list-item';

@Component({
  selector: 'app-macro-calendar-page',
  templateUrl: './macro-calendar-page.component.html'
})
export class MacroCalendarPageComponent implements OnInit {
  events: MacroEventListItem[] = [];
  activeBlocks: MacroEventListItem[] = [];
  isLoading = false;

  constructor(private macroCalendarService: MacroCalendarService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;

    const now = new Date();
    const to = new Date(now);
    to.setDate(now.getDate() + 7);

    this.macroCalendarService
      .getUpcomingEvents(now.toISOString(), to.toISOString())
      .subscribe({
        next: result => {
          this.events = result;
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
        }
      });

    this.macroCalendarService.getActiveBlocks().subscribe({
      next: result => this.activeBlocks = result
    });
  }
}
```

## 15.5 HTML example

```html
<div class="page">
  <h2>Macro Calendar</h2>

  <section *ngIf="activeBlocks.length > 0" class="active-blocks">
    <h3>Active Macro Blocks</h3>

    <div *ngFor="let block of activeBlocks" class="block-card">
      <div><strong>{{ block.title }}</strong></div>
      <div>{{ block.country }} · {{ block.currency }}</div>
      <div>Scheduled: {{ block.scheduledAtUtc | date:'medium' }}</div>
      <div>Window: {{ block.blockStartUtc | date:'shortTime' }} - {{ block.blockEndUtc | date:'shortTime' }}</div>
      <div>New entries blocked. Exits allowed.</div>
    </div>
  </section>

  <section>
    <h3>Upcoming Events</h3>

    <table>
      <thead>
        <tr>
          <th>Time</th>
          <th>Event</th>
          <th>Country</th>
          <th>Category</th>
          <th>Importance</th>
          <th>Forecast</th>
          <th>Previous</th>
          <th>Actual</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let event of events">
          <td>{{ event.scheduledAtUtc | date:'short' }}</td>
          <td>{{ event.title }}</td>
          <td>{{ event.country }}</td>
          <td>{{ event.category }}</td>
          <td>{{ event.importance }}</td>
          <td>{{ event.forecast }}</td>
          <td>{{ event.previous }}</td>
          <td>{{ event.actual }}</td>
        </tr>
      </tbody>
    </table>
  </section>
</div>
```

---

# 16. Suggested UI Behaviour

## Show these states clearly

### Event list row
- title
- country / currency
- scheduled time
- impact
- forecast / previous / actual
- badge if active block window exists

### Banner if blocked
- "High-impact macro event window active"
- "New entries disabled until 13:00 UTC"
- "Exits and reduce-only actions remain enabled"

### Optional filters
- currency
- importance
- category
- next 24h / 7d

---

# 17. Recommended Initial Event Filtering Policy

For crypto, block only high-value macro events at first.

## Suggested categories to block initially

- US CPI
- US PCE
- FOMC rate decisions
- Fed chair speeches
- Non-Farm Payrolls
- ECB / BoE rate decisions
- GDP
- major inflation releases

## Suggested ignored items at MVP

- low-impact regional releases
- niche countries with limited crypto relevance
- most medium-impact items unless strategy opts in

---

# 18. Registration in DI

```csharp
services.Configure<MacroCalendarOptions>(configuration.GetSection("MacroCalendar"));
services.Configure<FinnhubOptions>(configuration.GetSection("Finnhub"));

services.AddScoped<IMacroCalendarProvider, FinnhubMacroCalendarProvider>();
services.AddScoped<IMacroCalendarIngestionService, MacroCalendarIngestionService>();
services.AddScoped<IMacroBlockWindowCalculator, MacroBlockWindowCalculator>();
services.AddScoped<IRiskRule, MacroEventRiskRule>();

services.AddHostedService<MacroCalendarSyncWorker>();
```

---

# 19. Migration / Seeding Notes

## First migration

```bash
dotnet ef migrations add AddMacroCalendarTables
dotnet ef database update
```

## Optional seed data
Seed a few fake macro events in development so the UI and risk flow can be built before live API wiring is complete.

---

# 20. Logging and Observability

Log these clearly:

- provider sync start/end
- fetch count
- insert/update counts
- failed HTTP calls
- invalid payloads
- active macro block decisions in risk engine

## Example log events

- `MacroCalendar.SyncStarted`
- `MacroCalendar.SyncCompleted`
- `MacroCalendar.SyncFailed`
- `MacroCalendar.EntryBlocked`
- `MacroCalendar.ProviderUnavailable`

---

# 21. Failure Strategy

If the provider is down:

- do **not** crash the app
- keep using cached events
- raise warning state in UI
- optionally fail closed for very near high-impact windows
- log degraded mode clearly

## Suggested MVP rule
If last successful sync is older than 12 hours, mark macro protection as **degraded**.

---

# 22. Copilot Prompt Pack

Use these prompts one at a time inside your IDE.

---

## Prompt 1 — EF entities and configurations

```text
Create the EF Core entities and configurations for a macro calendar subsystem in .NET 8.

Requirements:
- Create MacroEvent entity
- Create MacroSyncRun entity
- Add enums MacroEventImportance and MacroEventStatus
- Add IEntityTypeConfiguration classes
- Add indexes for provider/providerEventId, scheduledAtUtc, blockStartUtc, blockEndUtc
- Use clean nullable reference types
- Target SQLite compatibility
- Return complete C# files
```

---

## Prompt 2 — Finnhub provider

```text
Create the infrastructure code for a Finnhub-based macro calendar provider in .NET 8.

Requirements:
- Create FinnhubOptions
- Create IFinnhubClient and FinnhubClient
- Create FinnhubEconomicCalendarItem response model
- Create IMacroCalendarProvider and FinnhubMacroCalendarProvider
- Map the Finnhub response into ExternalMacroEventDto
- Use HttpClient and System.Text.Json
- Add clean error handling and logging
- Return complete C# files
```

---

## Prompt 3 — ingestion service

```text
Create a MacroCalendarIngestionService in .NET 8.

Requirements:
- Pull normalized events from IMacroCalendarProvider
- Upsert MacroEvent rows using provider/providerEventId
- Map importance and status from raw strings
- Calculate block windows based on configurable policy
- Store raw payload JSON
- Return a MacroSyncResult with fetched/inserted/updated counts
- Use EF Core and async methods
- Return complete C# files
```

---

## Prompt 4 — background worker

```text
Create a BackgroundService called MacroCalendarSyncWorker in .NET 8.

Requirements:
- Read MacroCalendarOptions from IOptions
- Run a full sync every 6 hours
- Run incremental sync every 5 minutes
- Run faster sync every 60 seconds when a high-impact event is within 10 minutes
- Resolve scoped services correctly with IServiceScopeFactory
- Add robust logging and exception handling
- Return a complete C# file
```

---

## Prompt 5 — API controller and query service

```text
Create a query service and ASP.NET Core controller for a macro calendar subsystem.

Requirements:
- GET /api/macro-calendar/events
- GET /api/macro-calendar/active-blocks
- POST /api/macro-calendar/sync
- Create DTOs for list items
- Query by date range, currency, minimum importance
- Return clean async code using EF Core
- Return complete C# files
```

---

## Prompt 6 — risk rule

```text
Create a macro event risk rule for a trading app in .NET 8.

Requirements:
- Implement IRiskRule
- Block only new entries during active macro block windows
- Allow exits, reduce-only, stop-loss and take-profit actions
- Query MacroEvents from EF Core
- Return RiskCheckResult with allow/deny reason
- Return complete C# files
```

---

## Prompt 7 — Angular page

```text
Create an Angular feature page for a macro calendar list.

Requirements:
- Create MacroCalendarService using HttpClient
- Create MacroCalendarPageComponent
- Load upcoming events for the next 7 days
- Load active block windows
- Render a table of events and a highlighted active-block banner
- Use standalone simple Angular patterns and TypeScript interfaces
- Return the TypeScript, HTML, and SCSS files
```

---

# 23. Build Order

Use this order to keep the implementation smooth:

1. Create domain entities and migrations
2. Build Finnhub client and provider
3. Build ingestion service
4. Build manual sync endpoint
5. Test sync into DB
6. Build query endpoints
7. Build Angular page
8. Add background worker
9. Add risk rule integration
10. Add logging and degraded-mode handling

---

# 24. Recommended MVP Acceptance Criteria

## Backend
- Can sync upcoming macro events for the next 7 days
- Stores events locally with unique provider IDs
- Computes block windows for high-impact events
- Exposes events and active block windows through API

## Frontend
- Shows upcoming events in a sortable list/table
- Shows active block window banner
- Displays key event details clearly

## Risk Engine
- Blocks new entries during active windows
- Allows exits and sell-side risk reduction actions
- Returns a clear reason when blocking

## Ops
- Background worker keeps data fresh
- Provider failures are logged without crashing the app

---

# 25. Future Extensions

Once the MVP works, add these next:

- multiple providers behind same abstraction
- per-strategy macro policy
- whitelist / blacklist event categories
- user-defined block windows
- forecast surprise scoring
- post-event volatility cooldown
- UI countdown timers
- event relevance by asset class
- news + macro combined risk score

---

# 26. Final Recommendation

For your app, this is the cleanest MVP shape:

- **Finnhub provider behind abstraction**
- **Own normalized MacroEvent entity**
- **Background sync worker**
- **Simple Angular upcoming list**
- **Risk rule that blocks only new entries**
- **Exits always allowed**

That gives you a strong first version without coupling the whole system to one provider or overcomplicating the UI.

