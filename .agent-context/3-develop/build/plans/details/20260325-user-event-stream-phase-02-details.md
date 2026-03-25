<!-- markdownlint-disable-file -->

# Task Details: F7 — User Event Stream

## Phase 2: Backend — Stream Service & SignalR Relay

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, async/await, CancellationToken, structured logging
- `.github/instructions/dotnet-architecture.instructions.md` — BackgroundService in API project, DI registration
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions ≤ v6, Given_When_Then, callback capture pattern
- `.agent-context/0-knowledge/03-infrastructure-architecture.md` — Worker + API components, MarketDataStreamService as BackgroundService in API

## Design References

- `MarketDataStreamService` is the direct pattern source: same reconnection loop, exponential backoff parameters, SignalR broadcast via `IHubContext`
- Exponential backoff: 1s initial, 60s max, 20 retry cap (matching F4 parameters exactly)
- Wallet address obtained from `IHyperliquidSigner.WalletAddress` (singleton, already registered)
- All lifecycle events logged with Serilog structured logging: connect, disconnect, reconnect, subscribe, error

### Task 2.1: Create UserEventStreamService {#task-21-create-usereventstreamservice}

Create a `BackgroundService` that manages the user event WebSocket lifecycle: connect, subscribe, receive loop, reconnection with exponential backoff, and SignalR broadcast.

- **Complexity**: Medium
- **Risk Factors**: Reconnection loop must match F4 parameters exactly; must handle all error scenarios gracefully
- **Files**:
  - `src/TradingApp.Api/Services/UserEventStreamService.cs` - new file
- **Success**:
  - Service connects to Hyperliquid, subscribes to userEvents with wallet address
  - Fill events broadcast via `ReceiveFillEvent` SignalR method
  - Order updates broadcast via `ReceiveOrderUpdate` SignalR method
  - Connection status broadcast via `ReceiveUserConnectionStatus` SignalR method
  - Exponential backoff: 1s initial, 60s max, 20 retry cap
  - Auto-resubscribe after successful reconnect
  - All lifecycle events logged with structured Serilog logging
- **Dependencies**: Phase 1 (IHyperliquidUserEventClient, DTOs)

#### Implementation Details

```csharp
// src/TradingApp.Api/Services/UserEventStreamService.cs — new file
using Microsoft.AspNetCore.SignalR;
using TradingApp.Api.Hubs;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Api.Services;

/// <summary>
/// Background service managing the per-wallet Hyperliquid user event WebSocket.
/// Reconnects with exponential backoff and relays events to SignalR.
/// </summary>
public sealed class UserEventStreamService : BackgroundService
{
    private const int InitialBackoffMs = 1_000;
    private const int MaxBackoffMs = 60_000;
    private const int MaxRetryAttempts = 20;

    private readonly IHyperliquidUserEventClient _wsClient;
    private readonly IHubContext<MarketDataHub> _hubContext;
    private readonly IHyperliquidSigner _signer;
    private readonly ILogger<UserEventStreamService> _logger;

    private int _retryCount;

    public UserEventStreamService(
        IHyperliquidUserEventClient wsClient,
        IHubContext<MarketDataHub> hubContext,
        IHyperliquidSigner signer,
        ILogger<UserEventStreamService> logger)
    {
        _wsClient = wsClient;
        _hubContext = hubContext;
        _signer = signer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var walletAddress = _signer.WalletAddress;
        _logger.LogInformation(
            "UserEventStreamService starting for wallet {WalletAddress}",
            walletAddress);

        _wsClient.OnFillReceived(async fill =>
        {
            _logger.LogDebug(
                "Fill received: {Asset} {Side} {Size}@{Price}",
                fill.Asset, fill.Side, fill.Size, fill.Price);

            await _hubContext.Clients.All.SendAsync(
                "ReceiveFillEvent", fill, CancellationToken.None);
        });

        _wsClient.OnOrderUpdateReceived(async orderUpdate =>
        {
            _logger.LogDebug(
                "Order update received: {OrderId} {Asset} {Status}",
                orderUpdate.OrderId, orderUpdate.Asset, orderUpdate.Status);

            await _hubContext.Clients.All.SendAsync(
                "ReceiveOrderUpdate", orderUpdate, CancellationToken.None);
        });

        _wsClient.OnConnectionStateChanged(async state =>
        {
            _logger.LogInformation(
                "User event WebSocket state changed to {State}", state);

            var status = new ConnectionStatusDto
            {
                Source = "UserEvents",
                Status = state.ToString(),
                Detail = state == WebSocketConnectionState.Disconnected && _retryCount > 0
                    ? $"Retry {_retryCount}/{MaxRetryAttempts}"
                    : null,
                RetryCount = _retryCount
            };

            await _hubContext.Clients.All.SendAsync(
                "ReceiveUserConnectionStatus", status, CancellationToken.None);
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _wsClient.ConnectAsync(stoppingToken);
                await _wsClient.SubscribeToUserEventsAsync(walletAddress, stoppingToken);

                _retryCount = 0;
                _logger.LogInformation(
                    "Subscribed to userEvents for wallet {WalletAddress}", walletAddress);

                await _wsClient.ReceiveLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("UserEventStreamService stopping (cancellation requested)");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "User event WebSocket error (attempt {RetryCount}/{MaxRetries})",
                    _retryCount + 1, MaxRetryAttempts);
            }

            _retryCount++;

            if (_retryCount > MaxRetryAttempts)
            {
                _logger.LogError(
                    "User event WebSocket reconnection retries exhausted ({MaxRetries} attempts)",
                    MaxRetryAttempts);

                var disconnectedStatus = new ConnectionStatusDto
                {
                    Source = "UserEvents",
                    Status = "Disconnected",
                    Detail = $"Reconnection retries exhausted ({MaxRetryAttempts} attempts)",
                    RetryCount = _retryCount
                };

                await _hubContext.Clients.All.SendAsync(
                    "ReceiveUserConnectionStatus", disconnectedStatus, CancellationToken.None);

                break;
            }

            var backoffMs = Math.Min(
                InitialBackoffMs * (int)Math.Pow(2, _retryCount - 1),
                MaxBackoffMs);

            _logger.LogInformation(
                "User event WebSocket reconnecting in {BackoffMs}ms (attempt {RetryCount}/{MaxRetries})",
                backoffMs, _retryCount, MaxRetryAttempts);

            await Task.Delay(backoffMs, stoppingToken);
        }

        _logger.LogInformation("UserEventStreamService stopped");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UserEventStreamService shutting down");

        try
        {
            await _wsClient.DisconnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disconnecting user event WebSocket during shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}
```

##### Pattern References

- `src/TradingApp.Api/Services/MarketDataStreamService.cs` — direct pattern source: BackgroundService lifecycle, exponential backoff loop, IHubContext broadcast, StopAsync disconnect, structured logging

---

### Task 2.2: Register DI and configuration {#task-22-register-di-and-configuration}

Register the new user event client and stream service in the API's DI container.

- **Complexity**: Low
- **Risk Factors**: None — follows existing registration pattern
- **Files**:
  - `src/TradingApp.Api/Program.cs` - modification (add DI registrations)
- **Success**:
  - `IHyperliquidUserEventClient` registered as Singleton
  - `UserEventStreamService` registered as HostedService
  - Application starts without DI resolution errors
- **Dependencies**: Task 2.1

#### Implementation Details

```csharp
// src/TradingApp.Api/Program.cs — modification
// Add after the existing market data WebSocket registration:

// ... existing code ...
builder.Services.AddSingleton<IHyperliquidWebSocketClient, HyperliquidWebSocketClient>();
builder.Services.AddHostedService<MarketDataStreamService>();

// User event WebSocket — separate connection for per-wallet subscriptions
builder.Services.AddSingleton<IHyperliquidUserEventClient, HyperliquidUserEventClient>();
builder.Services.AddHostedService<UserEventStreamService>();
// ... existing code ...
```

Add required `using` statements:

```csharp
using TradingApp.Application.Abstractions.Services; // if not already present
using TradingApp.Api.Services; // if not already present
```

##### Pattern References

- `src/TradingApp.Api/Program.cs` — existing DI registration pattern: `AddSingleton<Interface, Implementation>()` + `AddHostedService<T>()`

---

### Task 2.3: Add unit tests for UserEventStreamService {#task-23-add-unit-tests-for-usereventstreamservice}

Create unit tests for the stream service, following the `MarketDataStreamServiceTests` pattern with mock chain and callback capture.

- **Complexity**: Medium
- **Risk Factors**: Async BackgroundService testing requires careful CancellationToken management
- **Files**:
  - `tests/TradingApp.Api.Tests/Services/UserEventStreamServiceTests.cs` - new file
- **Success**:
  - Tests cover: fill event relay to SignalR, order update relay to SignalR, connection status broadcast, reconnection on error
  - All tests pass
- **Dependencies**: Tasks 2.1, 2.2

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Services/UserEventStreamServiceTests.cs — new file
using Microsoft.AspNetCore.SignalR;
using TradingApp.Api.Hubs;
using TradingApp.Api.Services;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Api.Tests.Services;

[TestClass]
public sealed class UserEventStreamServiceTests
{
    private readonly Mock<IHyperliquidUserEventClient> _wsClientMock = new();
    private readonly Mock<IHubContext<MarketDataHub>> _hubContextMock = new();
    private readonly Mock<IHubClients> _hubClientsMock = new();
    private readonly Mock<IClientProxy> _clientProxyMock = new();
    private readonly Mock<IHyperliquidSigner> _signerMock = new();
    private readonly Mock<ILogger<UserEventStreamService>> _loggerMock = new();

    private const string TestWalletAddress = "0x1234567890abcdef1234567890abcdef12345678";

    [TestInitialize]
    public void Setup()
    {
        _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
        _hubClientsMock.Setup(c => c.All).Returns(_clientProxyMock.Object);
        _clientProxyMock
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _signerMock.Setup(s => s.WalletAddress).Returns(TestWalletAddress);
    }

    private UserEventStreamService CreateService()
    {
        return new UserEventStreamService(
            _wsClientMock.Object,
            _hubContextMock.Object,
            _signerMock.Object,
            _loggerMock.Object);
    }

    private void SetupLongRunningWebSocket()
    {
        _wsClientMock.Setup(w => w.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _wsClientMock.Setup(w => w.SubscribeToUserEventsAsync(TestWalletAddress, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _wsClientMock.Setup(w => w.ReceiveLoopAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) => await Task.Delay(Timeout.Infinite, ct));
        _wsClientMock.Setup(w => w.DisconnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [TestMethod]
    public async Task GivenStreamService_WhenStarted_ThenConnectsAndSubscribes()
    {
        // Arrange
        SetupLongRunningWebSocket();
        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        _wsClientMock.Verify(w => w.ConnectAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _wsClientMock.Verify(w => w.SubscribeToUserEventsAsync(TestWalletAddress, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task GivenStreamService_WhenFillReceived_ThenBroadcastsViaSignalR()
    {
        // Arrange
        SetupLongRunningWebSocket();
        Func<FillEventDto, Task>? fillHandler = null;
        _wsClientMock
            .Setup(w => w.OnFillReceived(It.IsAny<Func<FillEventDto, Task>>()))
            .Callback<Func<FillEventDto, Task>>(handler => fillHandler = handler);

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(200, cts.Token);

            fillHandler.Should().NotBeNull();
            await fillHandler!(new FillEventDto
            {
                Timestamp = DateTime.UtcNow,
                Asset = "BTC",
                Side = "Buy",
                Size = 0.1m,
                Price = 50000m,
                Fee = 0.5m,
                OrderId = "12345"
            });
        }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        _clientProxyMock.Verify(
            p => p.SendCoreAsync("ReceiveFillEvent", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenStreamService_WhenOrderUpdateReceived_ThenBroadcastsViaSignalR()
    {
        // Arrange
        SetupLongRunningWebSocket();
        Func<OrderUpdateDto, Task>? orderHandler = null;
        _wsClientMock
            .Setup(w => w.OnOrderUpdateReceived(It.IsAny<Func<OrderUpdateDto, Task>>()))
            .Callback<Func<OrderUpdateDto, Task>>(handler => orderHandler = handler);

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(200, cts.Token);

            orderHandler.Should().NotBeNull();
            await orderHandler!(new OrderUpdateDto
            {
                Timestamp = DateTime.UtcNow,
                OrderId = "67890",
                Asset = "ETH",
                Status = "Filled",
                FilledSize = 1.0m,
                RemainingSize = 0m
            });
        }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        _clientProxyMock.Verify(
            p => p.SendCoreAsync("ReceiveOrderUpdate", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Services/MarketDataStreamServiceTests.cs` — direct pattern source: mock chain setup, callback capture, async service start/stop, CancellationTokenSource timeout

---

### Task 2.4: Add SignalR hub integration test {#task-24-add-signalr-hub-integration-test}

Add an integration test verifying that the hub can accept connections when the user event stream service is registered.

- **Complexity**: Low
- **Risk Factors**: None — follows existing hub test pattern
- **Files**:
  - `tests/TradingApp.Api.Tests/Hubs/MarketDataHubTests.cs` - modification (add new test method and update existing test)
- **Success**:
  - Existing test updated to mock `IHyperliquidUserEventClient` (required to prevent DI resolution failure)
  - New test verifies hub still accepts connections with both market data and user event services registered
  - Existing hub tests continue to pass
- **Dependencies**: Task 2.2

#### Implementation Details

**Important**: After registering `IHyperliquidUserEventClient` as a singleton in `Program.cs` (Task 2.2), the existing `GivenSignalRHub_WhenClientConnects_ThenConnectionSucceeds` test will fail because `WebApplicationFactory` cannot resolve the new dependency. The existing test's `ConfigureServices` must also mock `IHyperliquidUserEventClient`.

**Update existing test** — add user event client mock to `ConfigureServices`:

```csharp
// tests/TradingApp.Api.Tests/Hubs/MarketDataHubTests.cs — modification
// In the existing GivenSignalRHub_WhenClientConnects_ThenConnectionSucceeds test,
// add IHyperliquidUserEventClient mock alongside the existing mocks:

var userEventClientMock = new Mock<IHyperliquidUserEventClient>();
userEventClientMock
    .Setup(w => w.ConnectAsync(It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);
userEventClientMock
    .Setup(w => w.SubscribeToUserEventsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);
userEventClientMock
    .Setup(w => w.ReceiveLoopAsync(It.IsAny<CancellationToken>()))
    .Returns<CancellationToken>(ct => Task.Delay(Timeout.Infinite, ct));

// Add inside ConfigureServices, after existing service replacements:
services.RemoveAll<IHyperliquidUserEventClient>();
services.AddSingleton(userEventClientMock.Object);
```

**Add new test method** — verifies hub accepts connections with both services:

```csharp
[TestMethod]
public async Task GivenSignalRHub_WhenUserEventStreamRegistered_ThenConnectionStillSucceeds()
{
    var wsClientMock = new Mock<IHyperliquidWebSocketClient>();
    var restClientMock = new Mock<IHyperliquidRestClient>();
    var userEventClientMock = new Mock<IHyperliquidUserEventClient>();

    restClientMock
        .Setup(r => r.GetMarketInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((MarketInfoDto?)null);

    wsClientMock
        .Setup(w => w.ConnectAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    wsClientMock
        .Setup(w => w.SubscribeToTradesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    wsClientMock
        .Setup(w => w.ReceiveLoopAsync(It.IsAny<CancellationToken>()))
        .Returns<CancellationToken>(ct => Task.Delay(Timeout.Infinite, ct));

    userEventClientMock
        .Setup(w => w.ConnectAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    userEventClientMock
        .Setup(w => w.SubscribeToUserEventsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    userEventClientMock
        .Setup(w => w.ReceiveLoopAsync(It.IsAny<CancellationToken>()))
        .Returns<CancellationToken>(ct => Task.Delay(Timeout.Infinite, ct));

    await using var factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
            builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
            builder.UseSetting("Hyperliquid:WsBaseUrl", "wss://api.hyperliquid-testnet.xyz/ws");
            builder.UseSetting("Hyperliquid:Network", "testnet");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHyperliquidWebSocketClient>();
                services.AddSingleton(wsClientMock.Object);

                services.RemoveAll<IHyperliquidRestClient>();
                services.AddSingleton(restClientMock.Object);

                services.RemoveAll<IHyperliquidUserEventClient>();
                services.AddSingleton(userEventClientMock.Object);
            });
        });

    var server = factory.Server;
    var hubConnection = new HubConnectionBuilder()
        .WithUrl(
            $"{server.BaseAddress}hubs/marketdata",
            options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
        .Build();

    await hubConnection.StartAsync();

    try
    {
        hubConnection.State.Should().Be(HubConnectionState.Connected);
    }
    finally
    {
        await hubConnection.StopAsync();
        await hubConnection.DisposeAsync();
    }
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Hubs/MarketDataHubTests.cs` — existing hub integration test with WebApplicationFactory, LongPolling transport

---

### Task 2.5: Run all backend tests {#task-25-run-all-backend-tests}

Build and run all backend test projects to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: No new files
- **Success**:
  - `dotnet build` succeeds for all projects
  - `dotnet test` passes for all test projects
  - All new tests from Phase 1 and Phase 2 pass
- **Dependencies**: Tasks 2.1–2.4

## Phase Success Criteria

- `UserEventStreamService` connects, subscribes, and runs the receive loop on startup
- Fill events are relayed to all SignalR clients via `ReceiveFillEvent` method
- Order updates are relayed to all SignalR clients via `ReceiveOrderUpdate` method
- Connection status changes broadcast via `ReceiveUserConnectionStatus` method
- Exponential backoff reconnection: 1s initial, 60s max, 20 retry cap
- Auto-resubscribe on successful reconnect
- Structured Serilog logging for connect, disconnect, reconnect, subscribe, error events
- DI registrations resolve correctly; application starts without errors
- All unit and integration tests pass; no regressions
