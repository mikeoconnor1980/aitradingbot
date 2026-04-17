using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;
using TradingApp.Infrastructure.Hyperliquid;
using TradingApp.Infrastructure.Hyperliquid.Models;

namespace TradingApp.Infrastructure.Services;

public sealed class HyperliquidWebSocketClient : IHyperliquidWebSocketClient
{
    private const int ReceiveBufferSize = 4096;
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(30);
    private static readonly byte[] PingPayload = Encoding.UTF8.GetBytes("{\"method\":\"ping\"}");

    private readonly ILogger<HyperliquidWebSocketClient> _logger;
    private readonly HyperliquidOptions _options;

    private ClientWebSocket? _webSocket;
    private Func<TradeTickDto, Task>? _tradeHandler;
    private Func<WebSocketConnectionState, Task>? _stateHandler;

    public HyperliquidWebSocketClient(
        ILogger<HyperliquidWebSocketClient> logger,
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
                try
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, "Reconnecting", cancellationToken);
                }
                catch (WebSocketException)
                {
                    // Remote already closed — safe to dispose
                }
            }

            _webSocket.Dispose();
        }

        _webSocket = new ClientWebSocket();

        var uri = new Uri(_options.WsBaseUrl);
        _logger.LogInformation("Connecting to Hyperliquid WebSocket at {Uri}", uri);

        await NotifyStateChangeAsync(WebSocketConnectionState.Connecting);
        await _webSocket.ConnectAsync(uri, cancellationToken);
        await NotifyStateChangeAsync(WebSocketConnectionState.Connected);

        _logger.LogInformation("Connected to Hyperliquid WebSocket");
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket is { State: WebSocketState.Open })
        {
            _logger.LogInformation("Disconnecting from Hyperliquid WebSocket");
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", cancellationToken);
        }

        await NotifyStateChangeAsync(WebSocketConnectionState.Disconnected);
    }

    public async Task SubscribeToTradesAsync(string coin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coin);

        if (_webSocket?.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected");
        }

        var request = new HyperliquidSubscribeRequest
        {
            Subscription = new HyperliquidSubscription
            {
                Coin = coin,
            },
        };

        var json = JsonSerializer.Serialize(request);
        var bytes = Encoding.UTF8.GetBytes(json);

        _logger.LogInformation("Subscribing to trades for {Coin}", coin);
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    public void OnTradeReceived(Func<TradeTickDto, Task> handler)
    {
        _tradeHandler = handler;
    }

    public void OnConnectionStateChanged(Func<WebSocketConnectionState, Task> handler)
    {
        _stateHandler = handler;
    }

    public async Task ReceiveLoopAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[ReceiveBufferSize];
        using var messageBuffer = new MemoryStream();

        _ = RunPingLoopAsync(cancellationToken);

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
                            "WebSocket close received: {Status} {Description}",
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
                _logger.LogWarning("WebSocket was disposed during receive");
                await NotifyStateChangeAsync(WebSocketConnectionState.Disconnected);
                return;
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "WebSocket error during receive");
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
            if (!doc.RootElement.TryGetProperty("channel", out var channelProp) ||
                !string.Equals(channelProp.GetString(), "trades", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var tradesMessage = JsonSerializer.Deserialize<HyperliquidTradesMessage>(json);
            if (tradesMessage?.Data is null || _tradeHandler is null)
            {
                return;
            }

            foreach (var trade in tradesMessage.Data)
            {
                if (!decimal.TryParse(trade.Px, NumberStyles.Float, CultureInfo.InvariantCulture, out var price) ||
                    !decimal.TryParse(trade.Sz, NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
                {
                    _logger.LogWarning("Skipping malformed trade: px={Px}, sz={Sz}", trade.Px, trade.Sz);
                    continue;
                }

                var dto = new TradeTickDto
                {
                    Asset = HyperliquidAssetMapper.ToDisplayName(trade.Coin),
                    Price = price,
                    Size = size,
                    Side = trade.Side,
                    TimestampMs = trade.Time,
                };

                await _tradeHandler(dto);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse WebSocket message");
        }
    }

    private async Task NotifyStateChangeAsync(WebSocketConnectionState state)
    {
        if (_stateHandler is not null)
        {
            await _stateHandler(state);
        }
    }

    private async Task RunPingLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(PingInterval, cancellationToken);

                var ws = _webSocket;
                if (ws?.State != WebSocketState.Open)
                {
                    break;
                }

                try
                {
                    await ws.SendAsync(
                        new ArraySegment<byte>(PingPayload),
                        WebSocketMessageType.Text,
                        true,
                        cancellationToken);
                }
                catch (WebSocketException ex)
                {
                    _logger.LogWarning(ex, "WebSocket ping failed — connection may be dead");
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
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