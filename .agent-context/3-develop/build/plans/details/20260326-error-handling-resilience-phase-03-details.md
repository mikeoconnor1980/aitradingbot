<!-- markdownlint-disable-file -->

# Task Details: F8 — Error Handling & Resilience

## Phase 3: Backend WebSocket Resilience

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — Async/await with `CancellationToken`, structured logging with named parameters
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions ≤ v6, Given/When/Then naming
- `.agent-context/0-knowledge/03-infrastructure-architecture.md` — WebSocket reconnection backoff parameters (1s initial, 60s max, 20 cap)
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — REST resync requirements after reconnect

## Design References

- PBI requires: "After WebSocket reconnect, open orders and positions are resynced via REST to ensure UI state is accurate"
- `MarketDataStreamService` already has `SeedStatsFromRestAsync()` but only calls it at startup
- `IHyperliquidAccountService` provides `GetOpenOrdersAsync()` and `GetPositionsAsync()` for REST resync
- `ConnectionStatusDto` already carries `Source`, `Status`, `Detail`, `RetryCount` via SignalR

### Task 3.1: Add REST state resync after WebSocket reconnection {#task-31-add-rest-state-resync-after-websocket-reconnection}

After a successful WebSocket reconnection, call REST endpoints to resync open orders, positions, and market stats, then push the updated state to the UI via SignalR.

- **Complexity**: Medium
- **Risk Factors**: REST resync must not block the WebSocket receive loop. If REST resync fails, the WebSocket connection should still proceed (resync is best-effort). Must avoid duplicate SignalR broadcasts that could cause UI flicker.
- **Files**:
  - `src/TradingApp.Api/Services/MarketDataStreamService.cs` — Modify: add `ResyncStateFromRestAsync` method, call it after successful reconnection
- **Success**:
  - After WebSocket reconnect, REST calls resync market stats (existing `SeedStatsFromRestAsync`)
  - After WebSocket reconnect, open orders and positions are fetched via `IHyperliquidAccountService` and broadcast via SignalR
  - REST resync failure is logged as warning but does not prevent WebSocket operation
  - `SeedStatsFromRestAsync` is also called after reconnect (not just at startup)
- **Dependencies**: Phase 1 and Phase 2 completed

#### Implementation Details

First, `MarketDataStreamService` needs access to `IHyperliquidAccountService`. Since it's a singleton `BackgroundService`, use `IServiceScopeFactory` to create a scope for the scoped service:

```csharp
// src/TradingApp.Api/Services/MarketDataStreamService.cs — modification

// Add to constructor parameters and field:
private readonly IServiceScopeFactory _scopeFactory;

// Constructor:
public MarketDataStreamService(
    IHyperliquidWebSocketClient wsClient,
    IHubContext<MarketDataHub> hubContext,
    IHyperliquidRestClient restClient,
    IServiceScopeFactory scopeFactory,
    ILogger<MarketDataStreamService> logger)
{
    _wsClient = wsClient;
    _hubContext = hubContext;
    _restClient = restClient;
    _scopeFactory = scopeFactory;
    _logger = logger;
}
```

Add the resync method:

```csharp
// src/TradingApp.Api/Services/MarketDataStreamService.cs — new method
private async Task ResyncStateFromRestAsync(CancellationToken cancellationToken)
{
    try
    {
        _logger.LogInformation("Starting REST state resync after WebSocket reconnection");

        // Resync market stats (existing method)
        await SeedStatsFromRestAsync(cancellationToken);

        // Resync orders and positions via scoped service
        using var scope = _scopeFactory.CreateScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IHyperliquidAccountService>();

        var ordersTask = accountService.GetOpenOrdersAsync(cancellationToken);
        var positionsTask = accountService.GetPositionsAsync(cancellationToken);

        await Task.WhenAll(ordersTask, positionsTask);

        await _hubContext.Clients.All.SendAsync(
            "ReceiveOrdersResync",
            await ordersTask,
            cancellationToken);

        await _hubContext.Clients.All.SendAsync(
            "ReceivePositionsResync",
            await positionsTask,
            cancellationToken);

        _logger.LogInformation(
            "REST state resync completed. Orders={OrderCount}, Positions={PositionCount}",
            (await ordersTask).Count,
            (await positionsTask).Count);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "REST state resync failed after WebSocket reconnection — UI state may be stale");
    }
}
```

Update the reconnect loop in `ExecuteAsync` to call resync:

```csharp
// src/TradingApp.Api/Services/MarketDataStreamService.cs — modification to ExecuteAsync
// After successful WebSocket connection + subscription restore:
// await _wsClient.ConnectAsync(stoppingToken);
// await _wsClient.SubscribeToTradesAsync(TargetCoin, stoppingToken);

// Add this block after subscription restore, before resetting retry count:
if (_retryCount > 0)
{
    // This is a reconnection, not initial connection — resync state
    await ResyncStateFromRestAsync(stoppingToken);
}

_retryCount = 0;
```

##### Pattern References

- `src/TradingApp.Api/Services/MarketDataStreamService.cs` — Existing `SeedStatsFromRestAsync` and reconnect loop
- `src/TradingApp.Api/Services/HyperliquidAccountService.cs` — `GetOpenOrdersAsync` and `GetPositionsAsync` methods

---

### Task 3.2: Emit Reconnecting state from backend WebSocket layer {#task-32-emit-reconnecting-state-from-backend-websocket-layer}

Emit the `WebSocketConnectionState.Reconnecting` state to SignalR clients before each reconnection attempt's backoff delay. Currently the backend transitions `Connected → Disconnected → Connected` with no `Reconnecting` intermediate state.

- **Complexity**: Low
- **Risk Factors**: Minimal — this is an addition to the existing `ConnectionStatusDto` broadcast pattern
- **Files**:
  - `src/TradingApp.Api/Services/MarketDataStreamService.cs` — Modify: broadcast `Reconnecting` state before backoff delay
- **Success**:
  - `ConnectionStatusDto` with `Status: "Reconnecting"` is broadcast on each retry attempt
  - Includes `RetryCount` and `Detail` with backoff duration
  - UI shows `Reconnecting` state (amber pulsing dot) during backoff — already supported by `AppComponent` CSS
- **Dependencies**: None (can be done alongside Task 3.1)

#### Implementation Details

```csharp
// src/TradingApp.Api/Services/MarketDataStreamService.cs — modification to ExecuteAsync
// In the reconnect loop, before the backoff delay:

_retryCount++;
if (_retryCount > MaxRetryAttempts)
{
    // existing exhaustion logic...
    break;
}

var backoffMs = Math.Min(
    InitialBackoffMs * (int)Math.Pow(2, _retryCount - 1),
    MaxBackoffMs);

// Add: Broadcast Reconnecting state before backoff
await _hubContext.Clients.All.SendAsync(
    "ReceiveConnectionStatus",
    new ConnectionStatusDto
    {
        Source = "Hyperliquid",
        Status = "Reconnecting",
        Detail = $"Reconnecting in {backoffMs / 1000}s (attempt {_retryCount}/{MaxRetryAttempts})",
        RetryCount = _retryCount,
    },
    stoppingToken);

_logger.LogWarning(
    "WebSocket disconnected. Reconnecting in {BackoffMs}ms (attempt {RetryCount}/{MaxRetries})",
    backoffMs,
    _retryCount,
    MaxRetryAttempts);

await Task.Delay(backoffMs, stoppingToken);
```

The broadcast uses inline `_hubContext.Clients.All.SendAsync("ReceiveConnectionStatus", ...)` — the same pattern already used in the reconnect-exhaustion block and the `OnConnectionStateChanged` callback.

##### Pattern References

- `src/TradingApp.Api/Services/MarketDataStreamService.cs` — Existing inline `SendAsync("ReceiveConnectionStatus", new ConnectionStatusDto { ... })` pattern (see retry-exhaustion block at line ~115)
- `src/TradingApp.Application/MarketData/Models/ConnectionStatusDto.cs` — `Source`, `Status`, `Detail`, `RetryCount` model

---

### Task 3.3: Add tests for reconnection with REST resync {#task-33-add-tests-for-reconnection-with-rest-resync}

Add tests verifying that REST resync is called after WebSocket reconnection and that `Reconnecting` state is broadcast during backoff.

- **Complexity**: Medium
- **Risk Factors**: Testing async BackgroundService behaviour requires careful timing with cancellation tokens. The `IServiceScopeFactory` mock adds constructor complexity.
- **Files**:
  - `tests/TradingApp.Api.Tests/Services/MarketDataStreamServiceTests.cs` — Modify: update `CreateService()` helper, add new test methods
- **Success**:
  - Test verifies `GetOpenOrdersAsync` and `GetPositionsAsync` are called on reconnection
  - Test verifies `ReceiveOrdersResync` and `ReceivePositionsResync` SignalR messages are sent
  - Test verifies `Reconnecting` state is broadcast before backoff
  - All existing tests still pass with updated constructor
- **Dependencies**: Tasks 3.1, 3.2

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Services/MarketDataStreamServiceTests.cs — modifications

// Add new mocks to test class:
private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
private readonly Mock<IServiceScope> _scopeMock = new();
private readonly Mock<IServiceProvider> _scopeProviderMock = new();
private readonly Mock<IHyperliquidAccountService> _accountServiceMock = new();

// Update TestInitialize:
[TestInitialize]
public void Setup()
{
    // ... existing hub mock setup ...

    _scopeMock.Setup(s => s.ServiceProvider).Returns(_scopeProviderMock.Object);
    _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);
    _scopeProviderMock
        .Setup(sp => sp.GetService(typeof(IHyperliquidAccountService)))
        .Returns(_accountServiceMock.Object);

    _accountServiceMock
        .Setup(a => a.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<OpenOrderDto>());
    _accountServiceMock
        .Setup(a => a.GetPositionsAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<PositionDto>());
}

// Update CreateService helper:
private MarketDataStreamService CreateService()
{
    return new MarketDataStreamService(
        _wsClientMock.Object,
        _hubContextMock.Object,
        _restClientMock.Object,
        _scopeFactoryMock.Object,
        _loggerMock.Object);
}

// New test:
[TestMethod]
public async Task GivenStreamService_WhenWebSocketReconnects_ThenResyncsOrdersAndPositions()
{
    var connectionCount = 0;

    _restClientMock
        .Setup(r => r.GetMarketInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((MarketInfoDto?)null);

    _wsClientMock
        .Setup(w => w.ConnectAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    _wsClientMock
        .Setup(w => w.SubscribeToTradesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    _wsClientMock
        .Setup(w => w.ReceiveLoopAsync(It.IsAny<CancellationToken>()))
        .Returns<CancellationToken>(ct =>
        {
            connectionCount++;
            if (connectionCount == 1)
                throw new InvalidOperationException("Connection lost");
            return Task.Delay(Timeout.Infinite, ct);
        });

    var service = CreateService();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    try
    {
        await service.StartAsync(cts.Token);
        await Task.Delay(3000, cts.Token);
    }
    catch (OperationCanceledException) { }
    finally
    {
        await service.StopAsync(CancellationToken.None);
    }

    _accountServiceMock.Verify(
        a => a.GetOpenOrdersAsync(It.IsAny<CancellationToken>()),
        Times.AtLeastOnce);
    _accountServiceMock.Verify(
        a => a.GetPositionsAsync(It.IsAny<CancellationToken>()),
        Times.AtLeastOnce);
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Services/MarketDataStreamServiceTests.cs` — Existing BackgroundService test patterns with cancellation token timing

## Phase Success Criteria

- After WebSocket reconnection, `GetOpenOrdersAsync` and `GetPositionsAsync` are called via REST
- Resynced orders and positions are pushed to SignalR clients via `ReceiveOrdersResync` and `ReceivePositionsResync`
- REST resync failure is logged as warning but does not prevent WebSocket operation
- `Reconnecting` state is broadcast via SignalR before each backoff delay
- `dotnet build TradingApp.sln` succeeds
- `dotnet test` passes for all projects
