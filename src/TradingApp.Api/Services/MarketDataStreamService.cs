using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TradingApp.Api.Hubs;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Api.Services;

/// <summary>
/// Background service that manages the Hyperliquid WebSocket connection,
/// aggregates trade data at 500ms intervals, and broadcasts via SignalR.
/// </summary>
public sealed class MarketDataStreamService : BackgroundService
{
    private const string TargetCoin = "BTC";
    private const string TargetAsset = "BTC-PERP";
    private const int MaxRetryAttempts = 20;
    private const int InitialBackoffMs = 1000;
    private const int MaxBackoffMs = 60000;

    private readonly IHyperliquidWebSocketClient _wsClient;
    private readonly IHubContext<MarketDataHub> _hubContext;
    private readonly IHyperliquidRestClient _restClient;
    private readonly ILogger<MarketDataStreamService> _logger;

    private readonly ConcurrentQueue<TradeTickDto> _tradeBuffer = new();
    private readonly TimeSpan _aggregationInterval = TimeSpan.FromMilliseconds(500);

    private decimal _lastPrice;
    private decimal _high24h;
    private decimal _low24h;
    private decimal _volume24h;
    private int _retryCount;

    public MarketDataStreamService(
        IHyperliquidWebSocketClient wsClient,
        IHubContext<MarketDataHub> hubContext,
        IHyperliquidRestClient restClient,
        ILogger<MarketDataStreamService> logger)
    {
        _wsClient = wsClient;
        _hubContext = hubContext;
        _restClient = restClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SeedStatsFromRestAsync(stoppingToken);

        _wsClient.OnTradeReceived(trade =>
        {
            _tradeBuffer.Enqueue(trade);
            return Task.CompletedTask;
        });

        _wsClient.OnConnectionStateChanged(async state =>
        {
            var status = new ConnectionStatusDto
            {
                Source = "Hyperliquid",
                Status = state.ToString(),
                RetryCount = _retryCount,
            };

            try
            {
                await _hubContext.Clients.All.SendAsync(
                    "ReceiveConnectionStatus",
                    status,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not broadcast connection state during shutdown");
            }
        });

        var aggregationTask = RunAggregationLoopAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _wsClient.ConnectAsync(stoppingToken);
                await _wsClient.SubscribeToTradesAsync(TargetCoin, stoppingToken);

                _retryCount = 0;

                await _wsClient.ReceiveLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket connection error");
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            _retryCount++;
            if (_retryCount > MaxRetryAttempts)
            {
                _logger.LogError(
                    "Max reconnection attempts ({MaxRetries}) exhausted. Stopping.",
                    MaxRetryAttempts);

                await _hubContext.Clients.All.SendAsync(
                    "ReceiveConnectionStatus",
                    new ConnectionStatusDto
                    {
                        Source = "Hyperliquid",
                        Status = "Disconnected",
                        Detail = $"Max reconnection attempts ({MaxRetryAttempts}) exhausted",
                        RetryCount = _retryCount,
                    },
                    stoppingToken);

                break;
            }

            var backoffMs = Math.Min(
                InitialBackoffMs * (int)Math.Pow(2, _retryCount - 1),
                MaxBackoffMs);

            _logger.LogWarning(
                "Reconnecting in {BackoffMs}ms (attempt {RetryCount}/{MaxRetries})",
                backoffMs,
                _retryCount,
                MaxRetryAttempts);

            await Task.Delay(backoffMs, stoppingToken);
        }

        await aggregationTask;
    }

    private async Task SeedStatsFromRestAsync(CancellationToken cancellationToken)
    {
        try
        {
            var marketInfo = await _restClient.GetMarketInfoAsync(TargetAsset, cancellationToken);
            if (marketInfo is null)
            {
                return;
            }

            _lastPrice = marketInfo.MidPrice;
            _high24h = marketInfo.MidPrice;
            _low24h = marketInfo.MidPrice;
            _volume24h = marketInfo.Volume24h;

            _logger.LogInformation(
                "Seeded 24h stats from REST: Price={Price}, Volume={Volume}",
                _lastPrice,
                _volume24h);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed 24h stats from REST. Starting with zeros.");
        }
    }

    private async Task RunAggregationLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_aggregationInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var tradesProcessed = 0;

            while (_tradeBuffer.TryDequeue(out var trade))
            {
                _lastPrice = trade.Price;
                _volume24h += trade.Price * trade.Size;

                if (trade.Price > _high24h)
                {
                    _high24h = trade.Price;
                }

                if (trade.Price < _low24h || _low24h == 0)
                {
                    _low24h = trade.Price;
                }

                tradesProcessed++;
            }

            if (tradesProcessed == 0)
            {
                continue;
            }

            var update = new PriceUpdateDto
            {
                Asset = TargetAsset,
                LastPrice = _lastPrice,
                High24h = _high24h,
                Low24h = _low24h,
                Volume24h = _volume24h,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };

            await _hubContext.Clients.All.SendAsync("ReceivePriceUpdate", update, cancellationToken);
        }
    }
}
