using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Worker.Services;

/// <summary>
/// Background service managing the per-wallet Hyperliquid user event WebSocket.
/// Reconnects with exponential backoff and relays events via ISignalRPublisher.
/// </summary>
public sealed class UserEventStreamService : BackgroundService
{
    private const int InitialBackoffMs = 1_000;
    private const int MaxBackoffMs = 60_000;
    private const int MaxRetryAttempts = 20;

    private readonly IHyperliquidUserEventClient _wsClient;
    private readonly ISignalRPublisher _publisher;
    private readonly ITelegramNotifier _telegramNotifier;
    private readonly NotificationConfigHolder _notificationConfig;
    private readonly IHyperliquidSigner _signer;
    private readonly ILogger<UserEventStreamService> _logger;

    private int _retryCount;

    public UserEventStreamService(
        IHyperliquidUserEventClient wsClient,
        ISignalRPublisher publisher,
        ITelegramNotifier telegramNotifier,
        NotificationConfigHolder notificationConfig,
        IHyperliquidSigner signer,
        ILogger<UserEventStreamService> logger)
    {
        _wsClient = wsClient;
        _publisher = publisher;
        _telegramNotifier = telegramNotifier;
        _notificationConfig = notificationConfig;
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
            _logger.LogInformation(
                "Fill received: {Asset} {Side} {Size}@{Price}",
                fill.Asset, fill.Side, fill.Size, fill.Price);

            // Relay individual fill to SignalR (for account state tracking in Angular)
            await _publisher.BroadcastFillEventAsync(fill);
        });

        _wsClient.OnFillBatchReceived(async fills =>
        {
            // Aggregate partial fills into consolidated notifications
            var groups = fills
                .GroupBy(f => new { f.Asset, f.Side, f.Direction })
                .Select(g => new
                {
                    g.Key.Asset,
                    g.Key.Side,
                    TotalSize = g.Sum(f => f.Size),
                    TotalPnl = g.Sum(f => f.ClosedPnl),
                    Vwap = g.Sum(f => f.Size * f.Price) / g.Sum(f => f.Size),
                    Count = g.Count(),
                })
                .ToList();

            foreach (var g in groups)
            {
                _logger.LogInformation(
                    "Fill batch: {Asset} {Side} total {Size}@{Vwap:N2} ({Count} partial fills, PnL={Pnl:N2})",
                    g.Asset, g.Side, g.TotalSize, g.Vwap, g.Count, g.TotalPnl);
            }

            if (_notificationConfig.TelegramChatId is { } chatId)
            {
                _logger.LogInformation("Sending consolidated Telegram fill notification to chat {ChatId}", chatId);
                await _telegramNotifier.NotifyFillBatchAsync(chatId, fills);
            }
            else
            {
                _logger.LogWarning("Fill batch received but no Telegram chat ID configured — notification skipped");
            }
        });

        _wsClient.OnOrderUpdateReceived(async orderUpdate =>
        {
            _logger.LogInformation(
                "Order update received: {OrderId} {Asset} {Status}",
                orderUpdate.OrderId, orderUpdate.Asset, orderUpdate.Status);

            await _publisher.BroadcastOrderUpdateAsync(orderUpdate);

            if (_notificationConfig.TelegramChatId is { } chatId)
            {
                var emoji = orderUpdate.Status switch
                {
                    "filled" => "\u2705",
                    "canceled" or "cancelled" => "\u274c",
                    "triggered" => "\u26a1",
                    _ => "\U0001f4cb",
                };
                var text = $"{emoji} *Order {orderUpdate.Status.ToUpperInvariant()}* \u2014 {orderUpdate.Asset}\n" +
                           $"OrderId: `{orderUpdate.OrderId}`";
                await _telegramNotifier.NotifyRiskEventAsync(chatId, $"Order {orderUpdate.Status}", text);
            }
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

            await _publisher.BroadcastUserConnectionStatusAsync(status);
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

                await _publisher.BroadcastUserConnectionStatusAsync(disconnectedStatus);

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

            await _publisher.BroadcastUserConnectionStatusAsync(reconnectingStatus);

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
