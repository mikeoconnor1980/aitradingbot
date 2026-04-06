using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;
using TradingApp.Application.Scheduling;
using TradingApp.Application.Scheduling.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;
using TradingApp.Infrastructure.Hyperliquid;

namespace TradingApp.Worker.Services;

/// <summary>
/// Core hosted service in the execution agent (Worker).
/// Connects to the Hyperliquid WebSocket trade stream, assembles candles from
/// trade ticks, and on each candle close runs the strategy evaluation pipeline
/// via <see cref="StrategyScheduler"/>.
/// </summary>
public sealed class LiveTradingService : BackgroundService
{
    private const int MaxRetryAttempts = 20;
    private const int InitialBackoffMs = 1_000;
    private const int MaxBackoffMs = 60_000;
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReadinessCheckInterval = TimeSpan.FromSeconds(10);

    private readonly IHyperliquidWebSocketClient _wsClient;
    private readonly CandleBuilder _candleBuilder;
    private readonly CandleClock _candleClock;
    private readonly IMarketContextBuilder _contextBuilder;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IGridController _gridController;
    private readonly IRiskEngine _riskEngine;
    private readonly IPositionManager _positionManager;
    private readonly ISignalController _signalController;
    private readonly IExecutionEngine _executionEngine;
    private readonly ISignerProvider _signerProvider;
    private readonly ITradingHealthProvider _healthProvider;
    private readonly StrategyConfig _strategyConfig;
    private readonly ILogger<LiveTradingService> _logger;

    private Candle? _latestOneHourCandle;
    private Candle? _latestFourHourCandle;
    private int _retryCount;
    private string _coin = string.Empty;

    public LiveTradingService(
        IHyperliquidWebSocketClient wsClient,
        CandleBuilder candleBuilder,
        CandleClock candleClock,
        IMarketContextBuilder contextBuilder,
        IStrategyEngine strategyEngine,
        IGridController gridController,
        IRiskEngine riskEngine,
        IPositionManager positionManager,
        ISignalController signalController,
        IExecutionEngine executionEngine,
        ISignerProvider signerProvider,
        ITradingHealthProvider healthProvider,
        IOptions<StrategyConfig> strategyConfig,
        ILogger<LiveTradingService> logger)
    {
        _wsClient = wsClient ?? throw new ArgumentNullException(nameof(wsClient));
        _candleBuilder = candleBuilder ?? throw new ArgumentNullException(nameof(candleBuilder));
        _candleClock = candleClock ?? throw new ArgumentNullException(nameof(candleClock));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _strategyEngine = strategyEngine ?? throw new ArgumentNullException(nameof(strategyEngine));
        _gridController = gridController ?? throw new ArgumentNullException(nameof(gridController));
        _riskEngine = riskEngine ?? throw new ArgumentNullException(nameof(riskEngine));
        _positionManager = positionManager ?? throw new ArgumentNullException(nameof(positionManager));
        _signalController = signalController ?? throw new ArgumentNullException(nameof(signalController));
        _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
        _signerProvider = signerProvider ?? throw new ArgumentNullException(nameof(signerProvider));
        _healthProvider = healthProvider ?? throw new ArgumentNullException(nameof(healthProvider));
        _strategyConfig = strategyConfig?.Value ?? throw new ArgumentNullException(nameof(strategyConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for wallet to be configured before starting the trading loop
        if (!_signerProvider.IsConfigured)
        {
            _logger.LogWarning(
                "Wallet not configured. Waiting for private key to be set via environment variable or runtime configuration...");

            while (!_signerProvider.IsConfigured && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(ReadinessCheckInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            _logger.LogInformation("Wallet configured. Starting trading loop.");
        }

        _coin = HyperliquidAssetMapper.ToCoin(_strategyConfig.Market);
        var triggerTimeframe = _strategyConfig.Timeframe;

        _logger.LogInformation(
            "LiveTradingService starting: Strategy={Strategy}, Market={Market}, Coin={Coin}, Timeframe={Timeframe}",
            _strategyConfig.StrategyName, _strategyConfig.Market, _coin, triggerTimeframe);

        var scheduler = new StrategyScheduler(
            _contextBuilder,
            _strategyEngine,
            _gridController,
            _riskEngine,
            _positionManager,
            _strategyConfig,
            triggerTimeframe,
            signalController: _signalController);

        _candleClock.CandleClosed += async evt =>
        {
            try
            {
                // Track latest higher-timeframe candles
                if (string.Equals(evt.Timeframe, "1h", StringComparison.OrdinalIgnoreCase))
                {
                    _latestOneHourCandle = evt.Candle;
                }

                if (string.Equals(evt.Timeframe, "4h", StringComparison.OrdinalIgnoreCase))
                {
                    _latestFourHourCandle = evt.Candle;
                }

                _healthProvider.RecordCandleClosed(evt.Timeframe);

                // Only call UpdateIndicators and trigger evaluation for the strategy timeframe
                if (string.Equals(evt.Timeframe, triggerTimeframe, StringComparison.OrdinalIgnoreCase))
                {
                    _contextBuilder.UpdateIndicators(evt.Candle);
                    await scheduler.HandleCandleClosedAsync(
                        evt, _latestOneHourCandle, _latestFourHourCandle, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing candle close: Symbol={Symbol}, Timeframe={Timeframe}, Timestamp={Timestamp}",
                    evt.Symbol, evt.Timeframe, evt.OpenTimeUtc);
            }
        };

        // Wire WebSocket → CandleBuilder
        _wsClient.OnTradeReceived(async trade =>
        {
            try
            {
                _healthProvider.RecordTradeReceived();
                await _candleBuilder.ProcessTickAsync(trade);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing trade tick: Asset={Asset}, Price={Price}",
                    trade.Asset, trade.Price);
            }
        });

        _wsClient.OnConnectionStateChanged(state =>
        {
            _healthProvider.RecordConnectionState(state == WebSocketConnectionState.Connected);
            _logger.LogInformation("WebSocket connection state: {State}", state);
            return Task.CompletedTask;
        });

        // WebSocket connection loop with exponential backoff
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _wsClient.ConnectAsync(stoppingToken);
                await _wsClient.SubscribeToTradesAsync(_coin, stoppingToken);

                _logger.LogInformation(
                    "WebSocket connected. Subscribed to {Coin} trade stream.", _coin);

                _retryCount = 0;
                await _wsClient.ReceiveLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket connection error for {Coin}", _coin);
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            _retryCount++;
            if (_retryCount > MaxRetryAttempts)
            {
                _logger.LogCritical(
                    "Max reconnection attempts ({MaxRetries}) exhausted. LiveTradingService stopping.",
                    MaxRetryAttempts);
                break;
            }

            var backoffMs = Math.Min(
                InitialBackoffMs * (int)Math.Pow(2, _retryCount - 1),
                MaxBackoffMs);

            _logger.LogWarning(
                "WebSocket disconnected. Reconnecting in {BackoffMs}ms (attempt {RetryCount}/{MaxRetries})",
                backoffMs, _retryCount, MaxRetryAttempts);

            await Task.Delay(backoffMs, stoppingToken);
        }

        _logger.LogInformation("LiveTradingService stopped.");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("LiveTradingService shutdown initiated. Cancelling open orders...");

        using var timeoutCts = new CancellationTokenSource(ShutdownTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            if (!string.IsNullOrEmpty(_coin))
            {
                var symbol = _strategyConfig.Market;
                await _executionEngine.CancelAllOrdersAsync(symbol, linkedCts.Token);
                _logger.LogInformation(
                    "All open orders cancelled for {Symbol} during shutdown.", symbol);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Shutdown order cancellation timed out after {Timeout}s.",
                ShutdownTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling orders during shutdown.");
        }

        try
        {
            await _wsClient.DisconnectAsync(linkedCts.Token);
            _logger.LogInformation("WebSocket disconnected during shutdown.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting WebSocket during shutdown.");
        }

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("LiveTradingService shutdown complete.");
    }
}
