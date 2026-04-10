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
        if (_signer is ISignerProvider provider && !provider.IsConfigured)
        {
            _logger.LogInformation(
                "UserEventStreamService skipped — no wallet configured on the control plane. " +
                "User event streaming runs on the execution agent.");
            return;
        }

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

            var reconnectingStatus = new ConnectionStatusDto
            {
                Source = "UserEvents",
                Status = WebSocketConnectionState.Reconnecting.ToString(),
                Detail = $"Reconnecting in {backoffMs}ms (attempt {_retryCount}/{MaxRetryAttempts})",
                RetryCount = _retryCount
            };

            await _hubContext.Clients.All.SendAsync(
                "ReceiveUserConnectionStatus", reconnectingStatus, CancellationToken.None);

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
