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
    private readonly HyperliquidOptions _options;

    private ClientWebSocket? _webSocket;
    private Func<FillEventDto, Task>? _fillHandler;
    private Func<OrderUpdateDto, Task>? _orderUpdateHandler;
    private Func<WebSocketConnectionState, Task>? _stateHandler;

    public HyperliquidUserEventClient(
        ILogger<HyperliquidUserEventClient> logger,
        IOptions<HyperliquidOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket is not null)
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "Reconnecting", cancellationToken);
            }

            _webSocket.Dispose();
        }

        _webSocket = new ClientWebSocket();

        var uri = new Uri(_options.WsBaseUrl);
        _logger.LogInformation("Connecting to Hyperliquid user event WebSocket at {Uri}", uri);

        await NotifyStateChangeAsync(WebSocketConnectionState.Connecting);
        await _webSocket.ConnectAsync(uri, cancellationToken);
        await NotifyStateChangeAsync(WebSocketConnectionState.Connected);

        _logger.LogInformation("Connected to Hyperliquid user event WebSocket");
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket is { State: WebSocketState.Open })
        {
            _logger.LogInformation("Disconnecting from Hyperliquid user event WebSocket");
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", cancellationToken);
        }

        await NotifyStateChangeAsync(WebSocketConnectionState.Disconnected);
    }

    public async Task SubscribeToUserEventsAsync(string walletAddress, CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected. Call ConnectAsync first.");
        }

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
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    public void OnFillReceived(Func<FillEventDto, Task> handler) => _fillHandler = handler;

    public void OnOrderUpdateReceived(Func<OrderUpdateDto, Task> handler) => _orderUpdateHandler = handler;

    public void OnConnectionStateChanged(Func<WebSocketConnectionState, Task> handler) => _stateHandler = handler;

    public async Task ReceiveLoopAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[ReceiveBufferSize];
        using var messageBuffer = new MemoryStream();

        while (!cancellationToken.IsCancellationRequested)
        {
            var ws = _webSocket;
            if (ws?.State != WebSocketState.Open)
            {
                break;
            }

            try
            {
                messageBuffer.SetLength(0);
                WebSocketReceiveResult result;

                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogWarning(
                            "User event WebSocket close received: {Status} {Description}",
                            result.CloseStatus,
                            result.CloseStatusDescription);
                        await NotifyStateChangeAsync(WebSocketConnectionState.Disconnected);
                        return;
                    }

                    await messageBuffer.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
                    await ProcessMessageAsync(json);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                _logger.LogWarning("User event WebSocket was disposed during receive");
                await NotifyStateChangeAsync(WebSocketConnectionState.Disconnected);
                return;
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "User event WebSocket error during receive");
                await NotifyStateChangeAsync(WebSocketConnectionState.Disconnected);
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

            foreach (var fill in eventsData.Fills)
            {
                if (_fillHandler is not null)
                {
                    var dto = MapFillToDto(fill);
                    await _fillHandler(dto);
                }
            }

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

    private static FillEventDto MapFillToDto(HyperliquidUserFill fill) =>
        new()
        {
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(fill.TimestampMs).UtcDateTime,
            Asset = fill.Coin,
            Side = MapOrderSide(fill.Side),
            Direction = fill.Direction,
            Size = decimal.TryParse(fill.Size, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var sz) ? sz : 0m,
            Price = decimal.TryParse(fill.Price, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var px) ? px : 0m,
            Fee = decimal.TryParse(fill.Fee, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var fee) ? fee : 0m,
            ClosedPnl = decimal.TryParse(fill.ClosedPnl, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var closedPnl) ? closedPnl : 0m,
            OrderId = fill.OrderId.ToString()
        };

    private static string MapOrderSide(string side)
    {
        return side.ToUpperInvariant() switch
        {
            "B" => "Buy",
            "A" => "Sell",
            _ => side,
        };
    }

    private static OrderUpdateDto MapOrderUpdateToDto(HyperliquidOrderUpdate update)
    {
        var origSz = decimal.TryParse(update.Order.OriginalSize, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var orig) ? orig : 0m;
        var currentSz = decimal.TryParse(update.Order.Size, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var curr) ? curr : 0m;
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

    private async Task NotifyStateChangeAsync(WebSocketConnectionState state)
    {
        if (_stateHandler is not null)
        {
            await _stateHandler(state);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_webSocket is not null)
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Disposing",
                        cts.Token);
                }
                catch (WebSocketException)
                {
                    // Ignore close errors during disposal.
                }
            }

            _webSocket.Dispose();
        }
    }
}
