using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Infrastructure.Hyperliquid;
using TradePilot.Infrastructure.Hyperliquid.Models;

namespace TradePilot.Infrastructure.Services;

public sealed class HyperliquidWebSocketClient : IHyperliquidWebSocketClient
{
    private const int ReceiveBufferSize = 4096;
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(30);
    private static readonly byte[] PingPayload = Encoding.UTF8.GetBytes("{\"method\":\"ping\"}");

    private readonly ILogger<HyperliquidWebSocketClient> _logger;
    private readonly HyperliquidOptions _options;
    private readonly IHyperliquidRestClient _restClient;
    private readonly ConcurrentDictionary<string, string> _subscriptionCoinCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _displayAssetCache = new(StringComparer.OrdinalIgnoreCase);

    private ClientWebSocket? _webSocket;
    private Func<TradeTickDto, Task>? _tradeHandler;
    private Func<WebSocketConnectionState, Task>? _stateHandler;

    public HyperliquidWebSocketClient(
        ILogger<HyperliquidWebSocketClient> logger,
        IOptions<HyperliquidOptions> options,
        IHyperliquidRestClient restClient)
    {
        _logger = logger;
        _options = options.Value;
        _restClient = restClient;
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

        var exchangeCoin = await ResolveSubscriptionCoinAsync(coin, cancellationToken);

        var request = new HyperliquidSubscribeRequest
        {
            Subscription = new HyperliquidSubscription
            {
                Coin = exchangeCoin,
            },
        };

        var json = JsonSerializer.Serialize(request);
        var bytes = Encoding.UTF8.GetBytes(json);

        _logger.LogInformation("Subscribing to trades for {Coin}", exchangeCoin);
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

                var asset = await ResolveDisplayAssetAsync(trade.Coin);

                var dto = new TradeTickDto
                {
                    Asset = asset,
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

    internal async Task<string> ResolveSubscriptionCoinAsync(string coin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(coin))
        {
            return string.Empty;
        }

        if (coin.StartsWith("@", StringComparison.Ordinal) || coin.Contains('/', StringComparison.Ordinal))
        {
            return coin;
        }

        if (_subscriptionCoinCache.TryGetValue(coin, out var cachedCoin))
        {
            return cachedCoin;
        }

        var perpResponse = await _restClient.PostInfoAsync<JsonElement>(new { type = "meta" }, cancellationToken);
        if (IsPerpCoin(perpResponse, coin))
        {
            _subscriptionCoinCache[coin] = coin;
            return coin;
        }

        var spotResponse = await _restClient.PostInfoAsync<JsonElement>(new { type = "spotMeta" }, cancellationToken);
        var spotCoin = ResolveSpotPairCoin(spotResponse, coin);
        _subscriptionCoinCache[coin] = spotCoin;
        return spotCoin;
    }

    internal async Task<string> ResolveDisplayAssetAsync(string exchangeCoin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exchangeCoin))
        {
            return string.Empty;
        }

        if (_displayAssetCache.TryGetValue(exchangeCoin, out var cachedAsset))
        {
            return cachedAsset;
        }

        if (!exchangeCoin.StartsWith("@", StringComparison.Ordinal) && !exchangeCoin.Contains('/', StringComparison.Ordinal))
        {
            var perpAsset = HyperliquidAssetMapper.ToDisplayName(exchangeCoin);
            _displayAssetCache[exchangeCoin] = perpAsset;
            return perpAsset;
        }

        var spotResponse = await _restClient.PostInfoAsync<JsonElement>(new { type = "spotMeta" }, cancellationToken);
        var spotAsset = ResolveSpotDisplayAsset(spotResponse, exchangeCoin);
        _displayAssetCache[exchangeCoin] = spotAsset;
        return spotAsset;
    }

    private static bool IsPerpCoin(JsonElement response, string coin)
    {
        if (!response.TryGetProperty("universe", out var universe) || universe.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in universe.EnumerateArray())
        {
            if (item.TryGetProperty("name", out var nameElement)
                && string.Equals(nameElement.GetString(), coin, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveSpotPairCoin(JsonElement response, string baseCoin)
    {
        var (tokens, universe) = GetSpotMetadataSections(response);
        var quoteTokenIndex = GetSpotTokenIndex(tokens, "USDC");
        var baseTokenIndex = GetSpotTokenIndex(tokens, baseCoin);

        foreach (var pair in universe.EnumerateArray())
        {
            if (!pair.TryGetProperty("tokens", out var pairTokens) || pairTokens.GetArrayLength() < 2)
            {
                continue;
            }

            if (pairTokens[0].GetInt32() != baseTokenIndex || pairTokens[1].GetInt32() != quoteTokenIndex)
            {
                continue;
            }

            if (pair.TryGetProperty("name", out var nameElement) && !string.IsNullOrWhiteSpace(nameElement.GetString()))
            {
                return nameElement.GetString()!;
            }

            if (pair.TryGetProperty("index", out var indexElement))
            {
                return $"@{indexElement.GetInt32()}";
            }

            break;
        }

        throw new InvalidOperationException($"Spot pair '{baseCoin}/USDC' not found in Hyperliquid spot metadata.");
    }

    private static string ResolveSpotDisplayAsset(JsonElement response, string exchangeCoin)
    {
        if (exchangeCoin.Contains('/', StringComparison.Ordinal))
        {
            return ToUsdMarket(exchangeCoin.Split('/')[0]);
        }

        var (tokens, universe) = GetSpotMetadataSections(response);
        var pairId = exchangeCoin.StartsWith("@", StringComparison.Ordinal)
            ? exchangeCoin[1..]
            : exchangeCoin;

        foreach (var pair in universe.EnumerateArray())
        {
            var matchesId = pair.TryGetProperty("index", out var indexElement)
                && string.Equals(indexElement.GetInt32().ToString(CultureInfo.InvariantCulture), pairId, StringComparison.OrdinalIgnoreCase);
            var matchesName = pair.TryGetProperty("name", out var nameElement)
                && string.Equals(nameElement.GetString(), exchangeCoin, StringComparison.OrdinalIgnoreCase);

            if (!matchesId && !matchesName)
            {
                continue;
            }

            if (!pair.TryGetProperty("tokens", out var pairTokens) || pairTokens.GetArrayLength() < 1)
            {
                break;
            }

            var baseTokenName = GetSpotTokenName(tokens, pairTokens[0].GetInt32());
            return ToUsdMarket(baseTokenName);
        }

        throw new InvalidOperationException($"Spot trade coin '{exchangeCoin}' not found in Hyperliquid spot metadata.");
    }

    private static (JsonElement Tokens, JsonElement Universe) GetSpotMetadataSections(JsonElement response)
    {
        if (!response.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Hyperliquid spot metadata did not include tokens.");
        }

        if (!response.TryGetProperty("universe", out var universe) || universe.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Hyperliquid spot metadata did not include universe.");
        }

        return (tokens, universe);
    }

    private static int GetSpotTokenIndex(JsonElement tokens, string tokenName)
    {
        foreach (var token in tokens.EnumerateArray())
        {
            if (!token.TryGetProperty("name", out var nameElement)
                || !string.Equals(nameElement.GetString(), tokenName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (token.TryGetProperty("index", out var indexElement))
            {
                return indexElement.GetInt32();
            }

            break;
        }

        throw new InvalidOperationException($"Spot token '{tokenName}' not found in Hyperliquid spot metadata.");
    }

    private static string GetSpotTokenName(JsonElement tokens, int tokenIndex)
    {
        foreach (var token in tokens.EnumerateArray())
        {
            if (!token.TryGetProperty("index", out var indexElement) || indexElement.GetInt32() != tokenIndex)
            {
                continue;
            }

            if (token.TryGetProperty("name", out var nameElement) && !string.IsNullOrWhiteSpace(nameElement.GetString()))
            {
                return nameElement.GetString()!;
            }

            break;
        }

        throw new InvalidOperationException($"Spot token index '{tokenIndex}' not found in Hyperliquid spot metadata.");
    }

    private static string ToUsdMarket(string baseCoin)
    {
        return $"{baseCoin}-USD";
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