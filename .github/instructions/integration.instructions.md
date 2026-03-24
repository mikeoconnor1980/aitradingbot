---
applyTo: "**/DTS.UKCT.Efiling.Integration/**/*.cs,**/Jobs/**/*.cs,**/IntegrationEvents/**/*.cs,**/EventServices/**/*.cs,**/SagaOrchestrators/**/*.cs,**/Sagas/**/*.cs,**/TimeoutEvents/**/*.cs"
---

# Integration Patterns (Jobs & Events)

The `DTS.UKCT.Efiling.Integration` project hosts background jobs and event handlers. Event/Job logic is defined in the Application project and executed by the Integration project.

<!-- DOT-CUSTOM-START:integration-custom -->
<!-- DOT-CUSTOM-END:integration-custom -->

## Job Pattern

Jobs run on a schedule or as one-off background processes. The `Job` base class is located in `DTS.UKCT.Efiling.Hosting`. Jobs run as a CronJob in Kubernetes

### Creating a Job

1. Create the job class in `DTS.UKCT.Efiling.Application/{BoundedContext}/Jobs/`:

```csharp
public sealed class MyCleanupJob : Job
{
    private readonly IRepository<MyEntity> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public MyCleanupJob(
        IRepository<MyEntity> repository,
        IUnitOfWork unitOfWork,
        ILogger<MyCleanupJob> logger,
        IHostApplicationLifetime hostApplicationLifetime) 
        : base(logger, hostApplicationLifetime)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting cleanup job");

        var expiredEntities = _repository.GetAll()
            .Where(e => e.CreatedDate < DateTime.UtcNow.AddDays(-90))
            .ToList();

        foreach (var entity in expiredEntities)
        {
            _repository.Delete(entity);
        }

        await _unitOfWork.CommitAsync();
        Logger.LogInformation("Cleanup job completed. Deleted {Count} entities", expiredEntities.Count);
    }
}
```

2. Configure in `DTS.UKCT.Efiling.Integration/appsettings.json`:

```json
{
  "Job": {
    "Enabled": true,
    "Name": "MyCleanupJob"
  },
  "EventHandler": {
    "Enabled": false
  }
}
```

3. Add to Helm chart `charts/integration/values.yaml`:

```yaml
jobs:
  my-cleanup-job:
    type: MyCleanupJob
    schedule: "0 6 * * *"  # Daily at 6am
```

## Timeout Event Pattern

Timeout events use the Event Bus to schedule recurring events at regular intervals. Should only be used if the frequency is 6 hours or less.

### Creating a Timeout Event Handler

1. Create the timeout event and handler in `DTS.UKCT.Efiling.Application/{BoundedContext}/TimeoutEvents/`:

```csharp
public sealed class MyRecurringTimeoutEvent : TimeoutEvent
{
    // Add any properties needed for the recurring task
}

public sealed class MyRecurringTimeoutEventHandler : BaseTimeoutEventHandler<MyRecurringTimeoutEvent>
{
    public MyRecurringTimeoutEventHandler(
        TelemetryClient telemetryClient,
        ILogger<MyRecurringTimeoutEventHandler> logger) 
        : base(telemetryClient, logger)
    {
    }

    protected override async Task OnHandleAsync(MyRecurringTimeoutEvent @event)
    {
        // Process the recurring task
        Logger.LogInformation("Processing recurring timeout event");
    }
}
```

2. Configure in `DTS.UKCT.Efiling.Integration/appsettings.json`:

```json
{
  "Scheduler": {
    "Enabled": true,
    "Handler": "MyRecurringTimeoutEventHandler",
    "TimeoutMinutes": 60
  },
  "Job": {
    "Enabled": false
  },
  "EventHandler": {
    "Enabled": false
  }
}
```

3. Add to Helm chart `charts/integration/values.yaml`:

```yaml
events:
  - name: my-recurring-scheduler
    type: MyRecurringTimeoutEventHandler
    timeoutMinutes: 60
```

## Integration Event Handler Pattern

Integration Event Handlers process messages asynchronously from the event bus.

### Creating an Integration Event

1. Create event and handler in `DTS.UKCT.Efiling.Application/{BoundedContext}/IntegrationEvents/`:

```csharp
// Event class
public sealed class MyEntityCreatedIntegrationEvent : RetryEvent
{
    public MyEntityCreatedIntegrationEvent(Guid entityId, string entityName)
    {
        EntityId = entityId;
        EntityName = entityName;
    }

    public Guid EntityId { get; }
    public string EntityName { get; }
}

// Handler in same file
public sealed class MyEntityCreatedIntegrationEventHandler 
    : BaseRetryEventHandler<MyEntityCreatedIntegrationEvent>
{
    private readonly IRepository<MyEntity> _repository;
    private readonly INotificationService _notificationService;

    public MyEntityCreatedIntegrationEventHandler(
        IRepository<MyEntity> repository,
        INotificationService notificationService,
        IEventBus eventBus,
        ILogger<MyEntityCreatedIntegrationEventHandler> logger) 
        : base(eventBus, logger)
    {
        _repository = repository;
        _notificationService = notificationService;
    }

    protected override async Task<bool> TryHandleAsync(
        MyEntityCreatedIntegrationEvent @event, 
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(@event.EntityId);
        if (entity == null)
        {
            Logger.LogWarning("Entity {EntityId} not found", @event.EntityId);
            return false; // Will retry
        }

        await _notificationService.SendAsync(new EntityCreatedNotification(entity));
        return true; // Success
    }
}
```

### Integration Event Types

- `Event` - Base event class
- `RetryEvent` - Event with retry support

### Handler Types

- `BaseEventHandler<TEvent>` - Simple handler with `HandleAsync`
- `BaseRetryEventHandler<TEvent>` - Handler with retry logic, uses `TryHandleAsync` returning bool

## Event Service Pattern

A hosted service must be created to subscribe and process events.

Create an event service in `DTS.UKCT.Efiling.Integration/EventServices/` to subscribe to events:

```csharp
public sealed class MyEventsService : IHostedLifecycleService
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<MyEventsService> _logger;

    public MyEventsService(IEventBus eventBus, ILogger<MyEventsService> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _eventBus.Subscribe<MyEntityCreatedIntegrationEvent, MyEntityCreatedIntegrationEventHandler>();
        _eventBus.Subscribe<MyEntityUpdatedIntegrationEvent, MyEntityUpdatedIntegrationEventHandler>();
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        return _eventBus.StartProcessingAsync(cancellationToken);
    }

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

## Helm Configuration

Add to `charts/integration/values.yaml`:

### Events with Auto-Scaling

```yaml
events:
  - name: my-entity-events
    type: MyEventsService
    scaleSubscription: MyEntityCreatedIntegrationEvent
    messageCount: 3
    minReplicas: 1
    maxReplicas: 5
    requests:
      memory: "200Mi"
      cpu: "200m"
    limits:
      memory: "400Mi"
      cpu: "500m"
```

### Scheduled Jobs

```yaml
jobs:
  my-cleanup-job:
    type: MyCleanupJob
    schedule: "0 6 * * *"  # Cron expression
```

<!-- DOT-CUSTOM-START:integration-publish-events -->
## Publishing Events

From commands or domain event handlers:

```csharp
// In a command handler
await _eventBus.PublishAsync(new MyEntityCreatedIntegrationEvent(entity.Id, entity.Name));
```
<!-- DOT-CUSTOM-END:integration-publish-events -->
