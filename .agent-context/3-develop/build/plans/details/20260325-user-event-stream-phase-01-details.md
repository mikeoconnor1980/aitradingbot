<!-- markdownlint-disable-file -->

# Task Details: F7 — User Event Stream

## Phase 1: Backend — User Event WebSocket Client & Models

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, static Create() factory, async/await with CancellationToken, private field `_` prefix
- `.github/instructions/dotnet-architecture.instructions.md` — interfaces in Application/Abstractions/Services/, implementations in Infrastructure/Services/, DTOs in Application layer
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions ≤ v6, Given_When_Then naming
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — userEvents subscription type, per-wallet authentication, shared vs per-user streams
- `.agent-context/0-knowledge/04-domain-model.md` — Fill, Order, Position entity definitions and relationships

## Design References

- Hyperliquid WebSocket API: `userEvents` subscription subscribes to fill and order update events for a specific wallet address
- Subscription message: `{ "method": "subscribe", "subscription": { "type": "userEvents", "user": "0x..." } }`
- Response channel name to be verified during implementation (expected: `"user"` or `"userEvents"`)

### Task 1.1: Create Hyperliquid user event infrastructure models {#task-11-create-hyperliquid-user-event-infrastructure-models}

Create infrastructure-layer deserialization models for Hyperliquid user event WebSocket messages. These handle the wire format from Hyperliquid and are internal to the Infrastructure project.

- **Complexity**: Medium
- **Risk Factors**: Hyperliquid message format is unverified; models may need adjustment during testing against live API
- **Files**:
  - `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidUserEventSubscription.cs` - new file, subscription request type with `user` field
  - `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidUserEventSubscribeRequest.cs` - new file, subscription request envelope (method + subscription)
  - `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidUserEventsMessage.cs` - new file, inbound user events message envelope
  - `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidUserEventsData.cs` - new file, data payload with fills and order updates arrays
  - `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidUserFill.cs` - new file, single fill record from WebSocket
  - `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidOrderUpdate.cs` - new file, single order update record from WebSocket
  - `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidOrderInfo.cs` - new file, order detail within an order update
- **Success**:
  - All models compile; JSON property names match expected Hyperliquid wire format
  - Subscribe request serializes correctly for userEvents subscription
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidUserEventSubscription.cs — new file
using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Subscription request for Hyperliquid userEvents WebSocket stream.
/// </summary>
internal sealed class HyperliquidUserEventSubscription
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "userEvents";

    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;
}
```

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidUserEventSubscribeRequest.cs — new file
using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Full subscribe request envelope for userEvents.
/// </summary>
internal sealed class HyperliquidUserEventSubscribeRequest
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "subscribe";

    [JsonPropertyName("subscription")]
    public HyperliquidUserEventSubscription Subscription { get; set; } = new();
}
```

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidUserEventsMessage.cs — new file
using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Inbound WebSocket message for the userEvents channel.
/// Expected format: { "channel": "user", "data": { "fills": [...], "orderUpdates": [...] } }
/// Note: Exact channel name and data shape to be verified against Hyperliquid API.
/// </summary>
internal sealed class HyperliquidUserEventsMessage
{
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public HyperliquidUserEventsData Data { get; set; } = new();
}
```

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidUserEventsData.cs — new file
using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Data payload within a userEvents WebSocket message containing fills and order updates.
/// </summary>
internal sealed class HyperliquidUserEventsData
{
    [JsonPropertyName("fills")]
    public List<HyperliquidUserFill> Fills { get; set; } = [];

    [JsonPropertyName("orderUpdates")]
    public List<HyperliquidOrderUpdate> OrderUpdates { get; set; } = [];
}
```

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidUserFill.cs — new file
using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Single fill record from the Hyperliquid userEvents WebSocket stream.
/// Note: Field names to be verified against Hyperliquid API documentation.
/// </summary>
internal sealed class HyperliquidUserFill
{
    [JsonPropertyName("coin")]
    public string Coin { get; set; } = string.Empty;

    [JsonPropertyName("px")]
    public string Price { get; set; } = string.Empty;

    [JsonPropertyName("sz")]
    public string Size { get; set; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("time")]
    public long TimestampMs { get; set; }

    [JsonPropertyName("fee")]
    public string Fee { get; set; } = string.Empty;

    [JsonPropertyName("oid")]
    public long OrderId { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("closedPnl")]
    public string ClosedPnl { get; set; } = string.Empty;

    [JsonPropertyName("dir")]
    public string Direction { get; set; } = string.Empty;
}
```

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidOrderUpdate.cs — new file
using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Single order update record from the Hyperliquid userEvents WebSocket stream.
/// Note: Field names to be verified against Hyperliquid API documentation.
/// </summary>
internal sealed class HyperliquidOrderUpdate
{
    [JsonPropertyName("order")]
    public HyperliquidOrderInfo Order { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("statusTimestamp")]
    public long StatusTimestamp { get; set; }
}
```

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidOrderInfo.cs — new file
using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Order detail within an order update from the Hyperliquid userEvents WebSocket stream.
/// </summary>
internal sealed class HyperliquidOrderInfo
{
    [JsonPropertyName("coin")]
    public string Coin { get; set; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("limitPx")]
    public string LimitPrice { get; set; } = string.Empty;

    [JsonPropertyName("sz")]
    public string Size { get; set; } = string.Empty;

    [JsonPropertyName("oid")]
    public long OrderId { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("origSz")]
    public string OriginalSize { get; set; } = string.Empty;
}
```

##### Pattern References

- `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidSubscribeRequest.cs` — existing subscribe request model pattern (method + subscription)
- `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidTradesMessage.cs` — existing inbound message deserialization pattern
- `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidTrade.cs` — existing single record model pattern with `[JsonPropertyName]`

---

### Task 1.2: Create application-layer DTOs for SignalR payloads {#task-12-create-application-layer-dtos-for-signalr-payloads}

Create DTOs in the Application layer that represent the SignalR payloads broadcast to Angular. These are the clean API-facing models, distinct from the infrastructure-layer Hyperliquid wire models.

- **Complexity**: Low
- **Risk Factors**: None — straightforward DTO creation
- **Files**:
  - `src/TradingApp.Application/MarketData/Models/FillEventDto.cs` - new file
  - `src/TradingApp.Application/MarketData/Models/OrderUpdateDto.cs` - new file
- **Success**:
  - DTOs compile and contain all fields specified in F7 PBI SignalR hub methods table
  - DTOs are in the Application layer (no Infrastructure dependency)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/MarketData/Models/FillEventDto.cs — new file
namespace TradingApp.Application.MarketData.Models;

/// <summary>
/// SignalR payload for fill events broadcast to Angular via ReceiveFillEvent.
/// </summary>
public sealed class FillEventDto
{
    public DateTime Timestamp { get; init; }
    public string Asset { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;
    public decimal Size { get; init; }
    public decimal Price { get; init; }
    public decimal Fee { get; init; }
    public string OrderId { get; init; } = string.Empty;
}
```

```csharp
// src/TradingApp.Application/MarketData/Models/OrderUpdateDto.cs — new file
namespace TradingApp.Application.MarketData.Models;

/// <summary>
/// SignalR payload for order update events broadcast to Angular via ReceiveOrderUpdate.
/// </summary>
public sealed class OrderUpdateDto
{
    public DateTime Timestamp { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public string Asset { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal FilledSize { get; init; }
    public decimal RemainingSize { get; init; }
}
```

##### Pattern References

- `src/TradingApp.Application/MarketData/Models/PriceUpdateDto.cs` — existing SignalR payload DTO pattern
- `src/TradingApp.Application/MarketData/Models/ConnectionStatusDto.cs` — existing status DTO pattern

---

### Task 1.3: Create IHyperliquidUserEventClient interface {#task-13-create-ihyperliquidusereventclient-interface}

Create the application-layer interface for the user event WebSocket client. Follows the same pattern as `IHyperliquidWebSocketClient` but scoped to user events.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IHyperliquidUserEventClient.cs` - new file
- **Success**:
  - Interface compiles and is in `Application/Abstractions/Services/`
  - Interface follows `IHyperliquidWebSocketClient` pattern with user-event-specific methods
- **Dependencies**: Task 1.2 (DTOs referenced by callback types)

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IHyperliquidUserEventClient.cs — new file
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// WebSocket client for Hyperliquid per-wallet user event subscriptions (fills, order updates).
/// Manages its own WebSocket connection, separate from the market data client.
/// </summary>
public interface IHyperliquidUserEventClient : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task SubscribeToUserEventsAsync(string walletAddress, CancellationToken cancellationToken = default);

    void OnFillReceived(Func<FillEventDto, Task> handler);

    void OnOrderUpdateReceived(Func<OrderUpdateDto, Task> handler);

    void OnConnectionStateChanged(Func<WebSocketConnectionState, Task> handler);

    Task ReceiveLoopAsync(CancellationToken cancellationToken = default);
}
```

##### Pattern References

- `src/TradingApp.Application/Abstractions/Services/IHyperliquidWebSocketClient.cs` — direct pattern source; same method structure, different subscription type

---

### Task 1.4: Implement HyperliquidUserEventClient {#task-14-implement-hyperliquidusereventclient}

Implement the user event WebSocket client. Manages its own `ClientWebSocket` connection, subscribes to `userEvents`, routes inbound messages to fill and order update handlers.

- **Complexity**: High
- **Risk Factors**: WebSocket frame handling, channel-based message dispatch, connection lifecycle management; Hyperliquid message format unverified
- **Files**:
  - `src/TradingApp.Infrastructure/Services/HyperliquidUserEventClient.cs` - new file
- **Success**:
  - Client connects to Hyperliquid WebSocket, sends userEvents subscribe request
  - Inbound messages are dispatched to fill or order update handlers based on content
  - Connection state changes emit via the state handler
  - Disconnect/dispose handled cleanly
- **Dependencies**: Tasks 1.1, 1.2, 1.3

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Services/HyperliquidUserEventClient.cs — new file
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;
using TradingApp.Infrastructure.Hyperliquid.Models;

namespace TradingApp.Infrastructure.Services;

/// <summary>
/// WebSocket client for Hyperliquid per-wallet user event subscriptions.
/// Manages its own connection separate from the market data WebSocket.
/// </summary>
public sealed class HyperliquidUserEventClient : IHyperliquidUserEventClient
{
    private const int ReceiveBufferSize = 4096;

    private readonly ILogger<HyperliquidUserEventClient> _logger;
    private readonly string _wsBaseUrl;
    private ClientWebSocket? _webSocket;

    private Func<FillEventDto, Task>? _fillHandler;
    private Func<OrderUpdateDto, Task>? _orderUpdateHandler;
    private Func<WebSocketConnectionState, Task>? _stateHandler;

    public HyperliquidUserEventClient(
        IOptions<HyperliquidOptions> options,
        ILogger<HyperliquidUserEventClient> logger)
    {
        _wsBaseUrl = options.Value.WsBaseUrl;
        _logger = logger;
    }

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _webSocket?.Dispose();
        _webSocket = new ClientWebSocket();

        _logger.LogInformation("Connecting to Hyperliquid user event WebSocket at {Url}", _wsBaseUrl);
        await EmitStateAsync(WebSocketConnectionState.Connecting);

        await _webSocket.ConnectAsync(new Uri(_wsBaseUrl), cancellationToken);

        _logger.LogInformation("Connected to Hyperliquid user event WebSocket");
        await EmitStateAsync(WebSocketConnectionState.Connected);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket is { State: WebSocketState.Open })
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", linked.Token);
        }

        _logger.LogInformation("Disconnected from Hyperliquid user event WebSocket");
        await EmitStateAsync(WebSocketConnectionState.Disconnected);
    }

    public async Task SubscribeToUserEventsAsync(string walletAddress, CancellationToken cancellationToken = default)
    {
        if (_webSocket is not { State: WebSocketState.Open })
            throw new InvalidOperationException("WebSocket is not connected. Call ConnectAsync first.");

        var request = new HyperliquidUserEventSubscribeRequest
        {
            Subscription = new HyperliquidUserEventSubscription
            {
                Type = "userEvents",
                User = walletAddress
            }
        };

        var json = JsonSerializer.Serialize(request);
        var bytes = Encoding.UTF8.GetBytes(json);

        _logger.LogInformation("Subscribing to userEvents for wallet {WalletAddress}", walletAddress);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    public void OnFillReceived(Func<FillEventDto, Task> handler) => _fillHandler = handler;

    public void OnOrderUpdateReceived(Func<OrderUpdateDto, Task> handler) => _orderUpdateHandler = handler;

    public void OnConnectionStateChanged(Func<WebSocketConnectionState, Task> handler) => _stateHandler = handler;

    public async Task ReceiveLoopAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[ReceiveBufferSize];

        while (_webSocket is { State: WebSocketState.Open } && !cancellationToken.IsCancellationRequested)
        {
            using var ms = new MemoryStream();

            try
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(buffer, cancellationToken);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("User event WebSocket received close frame");
                    await EmitStateAsync(WebSocketConnectionState.Disconnected);
                    return;
                }

                var json = Encoding.UTF8.GetString(ms.ToArray());
                await ProcessMessageAsync(json);
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "User event WebSocket error during receive");
                await EmitStateAsync(WebSocketConnectionState.Disconnected);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (ObjectDisposedException)
            {
                _logger.LogWarning("User event WebSocket disposed during receive");
                await EmitStateAsync(WebSocketConnectionState.Disconnected);
                return;
            }
        }
    }

    private async Task ProcessMessageAsync(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("channel", out var channelProp))
                return;

            var channel = channelProp.GetString();

            // Route based on channel — exact channel name to be verified against Hyperliquid API
            if (!string.Equals(channel, "user", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(channel, "userEvents", StringComparison.OrdinalIgnoreCase))
                return;

            if (!doc.RootElement.TryGetProperty("data", out var dataProp))
                return;

            var dataJson = dataProp.GetRawText();
            var eventsData = JsonSerializer.Deserialize<HyperliquidUserEventsData>(dataJson);

            if (eventsData is null)
                return;

            // Process fills
            foreach (var fill in eventsData.Fills)
            {
                if (_fillHandler is not null)
                {
                    var dto = MapFillToDto(fill);
                    await _fillHandler(dto);
                }
            }

            // Process order updates
            foreach (var orderUpdate in eventsData.OrderUpdates)
            {
                if (_orderUpdateHandler is not null)
                {
                    var dto = MapOrderUpdateToDto(orderUpdate);
                    await _orderUpdateHandler(dto);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize user event message; skipping");
        }
    }

    private static FillEventDto MapFillToDto(HyperliquidUserFill fill)
    {
        return new FillEventDto
        {
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(fill.TimestampMs).UtcDateTime,
            Asset = fill.Coin,
            Side = fill.Side,
            Size = decimal.TryParse(fill.Size, out var sz) ? sz : 0m,
            Price = decimal.TryParse(fill.Price, out var px) ? px : 0m,
            Fee = decimal.TryParse(fill.Fee, out var fee) ? fee : 0m,
            OrderId = fill.OrderId.ToString()
        };
    }

    private static OrderUpdateDto MapOrderUpdateToDto(HyperliquidOrderUpdate update)
    {
        var origSz = decimal.TryParse(update.Order.OriginalSize, out var orig) ? orig : 0m;
        var currentSz = decimal.TryParse(update.Order.Size, out var curr) ? curr : 0m;
        var filledSz = origSz - currentSz;

        return new OrderUpdateDto
        {
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(update.StatusTimestamp).UtcDateTime,
            OrderId = update.Order.OrderId.ToString(),
            Asset = update.Order.Coin,
            Status = update.Status,
            FilledSize = filledSz > 0 ? filledSz : 0m,
            RemainingSize = currentSz
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_webSocket is not null)
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing user event WebSocket during dispose");
                }
            }

            _webSocket.Dispose();
            _webSocket = null;
        }
    }

    private async Task EmitStateAsync(WebSocketConnectionState state)
    {
        if (_stateHandler is not null)
        {
            await _stateHandler(state);
        }
    }
}
```

##### Pattern References

- `src/TradingApp.Infrastructure/Services/HyperliquidWebSocketClient.cs` — direct pattern source: ClientWebSocket lifecycle, ReceiveLoopAsync buffer accumulation, ProcessMessageAsync channel routing, callback handler fields, DisposeAsync pattern

---

### Task 1.5: Add unit tests for HyperliquidUserEventClient {#task-15-add-unit-tests-for-hyperliquidusereventclient}

Create unit tests for the new user event client, following the existing `HyperliquidWebSocketClientTests` pattern.

- **Complexity**: Medium
- **Risk Factors**: Testing WebSocket behaviour without network; must verify state, handler registration, and validation
- **Files**:
  - `tests/TradingApp.Infrastructure.Tests/Services/HyperliquidUserEventClientTests.cs` - new file
- **Success**:
  - Tests cover: initial state (not connected), handler registration, subscribe pre-condition validation
  - All tests pass with `dotnet test`
- **Dependencies**: Tasks 1.3, 1.4

#### Implementation Details

```csharp
// tests/TradingApp.Infrastructure.Tests/Services/HyperliquidUserEventClientTests.cs — new file
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.MarketData.Models;
using TradingApp.Infrastructure.Services;

namespace TradingApp.Infrastructure.Tests.Services;

[TestClass]
public sealed class HyperliquidUserEventClientTests
{
    private readonly IOptions<HyperliquidOptions> _options = Options.Create(new HyperliquidOptions
    {
        BaseUrl = "https://api.hyperliquid-testnet.xyz",
        WsBaseUrl = "wss://api.hyperliquid-testnet.xyz/ws",
        Network = "testnet"
    });

    private readonly Mock<ILogger<HyperliquidUserEventClient>> _loggerMock = new();

    private HyperliquidUserEventClient CreateClient()
    {
        return new HyperliquidUserEventClient(_options, _loggerMock.Object);
    }

    [TestMethod]
    public void GivenNewClient_WhenCreated_ThenIsConnectedIsFalse()
    {
        // Arrange & Act
        var client = CreateClient();

        // Assert
        client.IsConnected.Should().BeFalse();
    }

    [TestMethod]
    public void GivenClient_WhenOnFillReceivedRegistered_ThenDoesNotThrow()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var act = () => client.OnFillReceived(_ => Task.CompletedTask);

        // Assert
        act.Should().NotThrow();
    }

    [TestMethod]
    public void GivenClient_WhenOnOrderUpdateReceivedRegistered_ThenDoesNotThrow()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var act = () => client.OnOrderUpdateReceived(_ => Task.CompletedTask);

        // Assert
        act.Should().NotThrow();
    }

    [TestMethod]
    public void GivenClient_WhenOnConnectionStateChangedRegistered_ThenDoesNotThrow()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var act = () => client.OnConnectionStateChanged(_ => Task.CompletedTask);

        // Assert
        act.Should().NotThrow();
    }

    [TestMethod]
    public async Task GivenNotConnected_WhenSubscribeToUserEvents_ThenThrowsInvalidOperationException()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var act = () => client.SubscribeToUserEventsAsync("0x1234567890abcdef");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not connected*");
    }
}
```

##### Pattern References

- `tests/TradingApp.Infrastructure.Tests/Services/HyperliquidWebSocketClientTests.cs` — direct pattern source: initial state tests, handler registration tests, pre-condition validation tests

---

### Task 1.6: Run all backend tests and verify no regressions {#task-16-run-all-backend-tests-and-verify-no-regressions}

Build all backend projects, run all test projects, and verify the new tests pass alongside existing tests.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: No new files
- **Success**:
  - `dotnet build` succeeds for all projects
  - `dotnet test` passes for all test projects
  - New `HyperliquidUserEventClientTests` pass
- **Dependencies**: Tasks 1.1–1.5

## Phase Success Criteria

- All Hyperliquid user event infrastructure models compile and serialize correctly
- Application-layer DTOs (FillEventDto, OrderUpdateDto) exist with all PBI-specified fields
- `IHyperliquidUserEventClient` interface is defined in Application/Abstractions/Services/
- `HyperliquidUserEventClient` implementation handles connect, subscribe, receive, disconnect, and dispose
- Message routing dispatches fills and order updates to separate callback handlers
- Unexpected message formats are logged as warnings and skipped (no crash)
- All unit tests pass; no regressions in existing test suite
