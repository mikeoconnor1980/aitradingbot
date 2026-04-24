<!-- markdownlint-disable-file -->

# Task Details: Hyperliquid Integration Code Review Remediation

## Phase 2: WebSocket Client Hardening

## Standards and Knowledge References

- **csharp.instructions.md**: `sealed` classes, `CancellationToken` propagation, `IDisposable`/`IAsyncDisposable` patterns
- **testing.instructions.md**: MSTest + Moq + FluentAssertions ≤ 6, `Given_When_Then` naming
- **02-hyperliquid-integration.md**: WebSocket architecture, market data stream, user event stream
- **30-worker-execution-pipeline.md**: TradingSession owns reconnection backoff (consumer-managed pattern)

## Design References

- `HyperliquidUserEventClient` already has a better pattern for ping loop CancellationToken — use `CancellationTokenSource.CreateLinkedTokenSource` to link the ping loop's lifetime to the receive loop's lifetime.
- WebSocket reconnection is consumer-managed (per architecture docs). These changes focus on cleanup, timeouts, and resource management — NOT auto-reconnection.

---

### Task 2.1: Link ping loop CancellationToken in `HyperliquidWebSocketClient` {#task-21-link-ping-loop-cancellationtoken}

Align the `HyperliquidWebSocketClient` ping loop with the `HyperliquidUserEventClient` pattern by using a linked `CancellationTokenSource`. (Review finding M5)

- **Complexity**: Medium
- **Risk Factors**: Must ensure the linked CTS is disposed properly; the ping loop must stop when the receive loop exits
- **Files**:
  - `src/TradePilot.Infrastructure/Services/HyperliquidWebSocketClient.cs` — modification
- **Success**:
  - Ping loop uses a linked CancellationTokenSource that cancels when ReceiveLoopAsync exits
  - No orphaned ping tasks after disconnect
  - Linked CTS is disposed after ReceiveLoopAsync completes
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Services/HyperliquidWebSocketClient.cs — modification
// In ReceiveLoopAsync, wrap the ping loop launch with a linked CTS

    public async Task ReceiveLoopAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[ReceiveBufferSize];
        using var messageBuffer = new MemoryStream();

        // Link ping loop lifetime to the receive loop — when ReceiveLoopAsync exits,
        // the ping loop is automatically cancelled via the linked CTS.
        using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = RunPingLoopAsync(pingCts.Token);

        // ... rest of ReceiveLoopAsync remains unchanged ...
```

When `ReceiveLoopAsync` exits (break, return, or exception), the `using` on `pingCts` will dispose and cancel the linked token, stopping `RunPingLoopAsync`.

##### Pattern References

- `src/TradePilot.Infrastructure/Services/HyperliquidUserEventClient.cs` — `ReceiveLoopAsync` uses `CancellationTokenSource.CreateLinkedTokenSource` for the identical pattern
- `src/TradePilot.Infrastructure/Services/HyperliquidWebSocketClient.cs` — current code at line `_ = RunPingLoopAsync(cancellationToken);`

---

### Task 2.2: Add connect timeout to both WebSocket clients {#task-22-add-connect-timeout-to-websocket-clients}

Wrap `ConnectAsync` calls with a timeout CancellationTokenSource to prevent indefinite hangs when the Hyperliquid WebSocket endpoint is unreachable. (Review finding m5)

- **Complexity**: Medium
- **Risk Factors**: Must use `CreateLinkedTokenSource` to respect caller's cancellation AND timeout; timeout value should be configurable but defaults to 15 seconds
- **Files**:
  - `src/TradePilot.Infrastructure/Services/HyperliquidWebSocketClient.cs` — modification
  - `src/TradePilot.Infrastructure/Services/HyperliquidUserEventClient.cs` — modification
- **Success**:
  - Both clients' `ConnectAsync` methods use a 15-second timeout
  - Caller's `CancellationToken` is still respected (linked source)
  - Both throw `OperationCanceledException` on timeout
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Services/HyperliquidWebSocketClient.cs — modification
// In ConnectAsync, add timeout via linked CTS

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

    public async Task ConnectAsync(string exchangeCoin, CancellationToken cancellationToken = default)
    {
        // ... existing socket cleanup code ...

        _webSocket = new ClientWebSocket();

        var uri = new Uri($"{_options.WsBaseUrl}");
        _logger.LogInformation("Connecting to Hyperliquid WebSocket at {Uri}", uri);

        await NotifyStateChangeAsync(WebSocketConnectionState.Connecting);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ConnectTimeout);
        await _webSocket.ConnectAsync(uri, timeoutCts.Token);

        await NotifyStateChangeAsync(WebSocketConnectionState.Connected);
        // ... rest of ConnectAsync ...
    }
```

```csharp
// src/TradePilot.Infrastructure/Services/HyperliquidUserEventClient.cs — modification
// In ConnectAsync, add timeout via linked CTS

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        // ... existing socket cleanup code ...

        _webSocket = new ClientWebSocket();

        var uri = new Uri(_options.WsBaseUrl);
        _logger.LogInformation("Connecting to Hyperliquid user event WebSocket at {Uri}", uri);

        await NotifyStateChangeAsync(WebSocketConnectionState.Connecting);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ConnectTimeout);
        await _webSocket.ConnectAsync(uri, timeoutCts.Token);

        await NotifyStateChangeAsync(WebSocketConnectionState.Connected);
        // ... rest of ConnectAsync ...
    }
```

##### Pattern References

- `src/TradePilot.Infrastructure/Services/HyperliquidWebSocketClient.cs` — current `ConnectAsync`
- `src/TradePilot.Infrastructure/Services/HyperliquidUserEventClient.cs` — current `ConnectAsync`

---

### Task 2.3: Increase receive buffer size {#task-23-increase-receive-buffer-size}

Increase `ReceiveBufferSize` from 4096 to 8192 in both WebSocket clients to reduce the number of `ReceiveAsync` calls needed for large batch messages (e.g., multi-fill events from grid deploy). (Review finding m6)

- **Complexity**: Low
- **Risk Factors**: None — trivial constant change, existing accumulation loop handles multi-frame messages
- **Files**:
  - `src/TradePilot.Infrastructure/Services/HyperliquidWebSocketClient.cs` — modification
  - `src/TradePilot.Infrastructure/Services/HyperliquidUserEventClient.cs` — modification
- **Success**:
  - Both files have `ReceiveBufferSize = 8192`
- **Dependencies**: None

---

### Task 2.4: Update WebSocket tests {#task-24-update-websocket-tests}

Add or update tests to verify the new connect timeout and ping loop CancellationToken linking behavior.

- **Complexity**: Medium
- **Risk Factors**: WebSocket tests require mocking `ClientWebSocket` which is non-trivial; existing test patterns may need adaptation
- **Files**:
  - `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidWebSocketClientTests.cs` — modification
  - `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidUserEventClientTests.cs` — modification
- **Success**:
  - Tests verify that buffer size constant is 8192
  - Existing tests continue to pass with the linked CTS and timeout changes
- **Dependencies**: Tasks 2.1–2.3

#### Implementation Details

```csharp
// tests/TradePilot.Infrastructure.Tests/Services/HyperliquidWebSocketClientTests.cs — add test

    [TestMethod]
    public void GivenWebSocketClient_WhenCheckingBufferSize_ThenIs8192()
    {
        // Verify the buffer size constant is 8192 via reflection or by checking
        // the behavior of ReceiveLoopAsync with a payload > 4096 bytes.
        // Implementation: read the ReceiveBufferSize private constant.
        var field = typeof(HyperliquidWebSocketClient)
            .GetField("ReceiveBufferSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        field.Should().NotBeNull();
        var value = (int)field!.GetValue(null)!;
        value.Should().Be(8192);
    }
```

##### Pattern References

- `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidWebSocketClientTests.cs` — existing test structure
- `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidUserEventClientTests.cs` — existing test structure

---

### Task 2.5: Build and run all tests {#task-25-build-and-run-all-tests}

Build the solution and run WebSocket-related tests to verify Phase 2 changes.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradePilot.sln` succeeds
  - `dotnet test tests/TradePilot.Infrastructure.Tests/ --filter "FullyQualifiedName~WebSocket"` passes
  - `dotnet test tests/TradePilot.Infrastructure.Tests/ --filter "FullyQualifiedName~UserEvent"` passes
- **Dependencies**: Tasks 2.1–2.4

## Phase Success Criteria

- `HyperliquidWebSocketClient` ping loop uses linked `CancellationTokenSource` (no orphaned tasks)
- Both WebSocket clients have 15-second connect timeout
- Both WebSocket clients use 8192-byte receive buffer
- All existing and new WebSocket tests pass
