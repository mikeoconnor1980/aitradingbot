using Microsoft.Extensions.Logging;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;
using TradingApp.Application.Scheduling;
using TradingApp.Application.Scheduling.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Domain.Entities;
using TradingApp.Infrastructure.Hyperliquid;

namespace TradingApp.Worker.Services;

/// <summary>
/// On-demand trading session. Created and started by <see cref="AgentCheckInService"/>
/// when the dashboard issues a Start command. Stopped when the dashboard issues a Stop
/// command or the service host shuts down.
/// </summary>
public sealed class TradingSession : IAsyncDisposable
{
    private const int MaxRetryAttempts = 20;
    private const int InitialBackoffMs = 1_000;
    private const int MaxBackoffMs = 60_000;
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    private readonly IHyperliquidWebSocketClient _wsClient;
    private readonly IHyperliquidUserEventClient _userEventClient;
    private readonly CandleBuilder _candleBuilder;
    private readonly CandleClock _candleClock;
    private readonly IMarketContextBuilder _contextBuilder;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IGridController _gridController;
    private readonly IRiskEngine _riskEngine;
    private readonly IPositionManager _positionManager;
    private readonly ISignalController _signalController;
    private readonly IExecutionEngine _executionEngine;
    private readonly IFillProcessor _fillProcessor;
    private readonly ISignerProvider _signerProvider;
    private readonly IStateRecoveryService? _stateRecoveryService;
    private readonly IOrderTracker _orderTracker;
    private readonly ITradingHealthProvider _healthProvider;
    private readonly ILogger _logger;

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private int _retryCount;

    public StrategyConfig StrategyConfig { get; }
    public GridState GridState { get; }
    public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;
    public bool IsRunning => _runTask is not null && !_runTask.IsCompleted;

    public TradingSession(
        StrategyConfig strategyConfig,
        IHyperliquidWebSocketClient wsClient,
        IHyperliquidUserEventClient userEventClient,
        CandleBuilder candleBuilder,
        CandleClock candleClock,
        IMarketContextBuilder contextBuilder,
        IStrategyEngine strategyEngine,
        IGridController gridController,
        IRiskEngine riskEngine,
        IPositionManager positionManager,
        ISignalController signalController,
        IExecutionEngine executionEngine,
        IFillProcessor fillProcessor,
        ISignerProvider signerProvider,
        ITradingHealthProvider healthProvider,
        ILogger logger,
        GridState? gridState = null,
        IStateRecoveryService? stateRecoveryService = null,
        IOrderTracker? orderTracker = null)
    {
        StrategyConfig = strategyConfig;
        _wsClient = wsClient;
        _userEventClient = userEventClient;
        _candleBuilder = candleBuilder;
        _candleClock = candleClock;
        _contextBuilder = contextBuilder;
        _strategyEngine = strategyEngine;
        _gridController = gridController;
        _riskEngine = riskEngine;
        _positionManager = positionManager;
        _signalController = signalController;
        _executionEngine = executionEngine;
        _fillProcessor = fillProcessor;
        _signerProvider = signerProvider;
        _stateRecoveryService = stateRecoveryService;
        _orderTracker = orderTracker!;
        _healthProvider = healthProvider;
        _logger = logger;
        GridState = gridState ?? new GridState();
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _runTask = RunAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts is null || _runTask is null) return;

        _logger.LogInformation("TradingSession stop requested. Cancelling open orders...");

        await _cts.CancelAsync();

        try
        {
            await _runTask.WaitAsync(ShutdownTimeout);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Trading session did not stop within {Timeout}s.", ShutdownTimeout.TotalSeconds);
        }
        catch (OperationCanceledException) { }

        // Cancel open orders
        using var timeoutCts = new CancellationTokenSource(ShutdownTimeout);
        try
        {
            await _executionEngine.CancelAllOrdersAsync(StrategyConfig.Market, timeoutCts.Token);
            _logger.LogInformation("All open orders cancelled for {Symbol}.", StrategyConfig.Market);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling orders during session stop.");
        }

        // Disconnect WebSocket
        try
        {
            await _wsClient.DisconnectAsync(timeoutCts.Token);
            _logger.LogInformation("WebSocket disconnected.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting WebSocket during session stop.");
        }

        // Disconnect user event WebSocket
        try
        {
            await _userEventClient.DisconnectAsync(timeoutCts.Token);
            _logger.LogInformation("User event WebSocket disconnected.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting user event WebSocket during session stop.");
        }

        _logger.LogInformation("TradingSession stopped.");
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var coin = HyperliquidAssetMapper.ToCoin(StrategyConfig.Market);
        var triggerTimeframe = StrategyConfig.Timeframe;

        _logger.LogInformation(
            "TradingSession starting: Strategy={Strategy}, Market={Market}, Coin={Coin}, Timeframe={Timeframe}",
            StrategyConfig.StrategyName, StrategyConfig.Market, coin, triggerTimeframe);

        // Attempt state recovery from DB + Hyperliquid
        if (_stateRecoveryService is not null && _signerProvider.IsConfigured && _orderTracker is not null)
        {
            try
            {
                var recoveredState = await _stateRecoveryService.RecoverAsync(
                    StrategyConfig.StrategyName,
                    StrategyConfig.Market,
                    _signerProvider.WalletAddress,
                    _orderTracker,
                    stoppingToken);

                // Copy recovered values into our shared GridState
                GridState.Lifecycle = recoveredState.Lifecycle;
                GridState.GridCycleId = recoveredState.GridCycleId;
                GridState.FilledLevels = recoveredState.FilledLevels;
                GridState.TotalLevels = recoveredState.TotalLevels;
                GridState.TrailingStopHighWatermark = recoveredState.TrailingStopHighWatermark;
                GridState.CandlesSinceEntry = recoveredState.CandlesSinceEntry;

                _logger.LogInformation(
                    "State recovery complete: Lifecycle={Lifecycle}, FilledLevels={Filled}/{Total}",
                    GridState.Lifecycle, GridState.FilledLevels, GridState.TotalLevels);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "State recovery failed. Starting with fresh state. Strategy={Strategy}",
                    StrategyConfig.StrategyName);
            }
        }

        Candle? latestOneHourCandle = null;
        Candle? latestFourHourCandle = null;

        var scheduler = new StrategyScheduler(
            _contextBuilder,
            _strategyEngine,
            _gridController,
            _riskEngine,
            _positionManager,
            StrategyConfig,
            triggerTimeframe,
            signalController: _signalController,
            gridState: GridState);

        _candleClock.CandleClosed += async evt =>
        {
            try
            {
                if (string.Equals(evt.Timeframe, "1h", StringComparison.OrdinalIgnoreCase))
                    latestOneHourCandle = evt.Candle;

                if (string.Equals(evt.Timeframe, "4h", StringComparison.OrdinalIgnoreCase))
                    latestFourHourCandle = evt.Candle;

                _healthProvider.RecordCandleClosed(evt.Timeframe);

                if (string.Equals(evt.Timeframe, triggerTimeframe, StringComparison.OrdinalIgnoreCase))
                {
                    _contextBuilder.UpdateIndicators(evt.Candle);
                    await scheduler.HandleCandleClosedAsync(
                        evt, latestOneHourCandle, latestFourHourCandle, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing candle close: Symbol={Symbol}, Timeframe={Timeframe}",
                    evt.Symbol, evt.Timeframe);
            }
        };

        _wsClient.OnTradeReceived(async trade =>
        {
            try
            {
                _healthProvider.RecordTradeReceived();
                await _candleBuilder.ProcessTickAsync(trade);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing trade tick: Asset={Asset}", trade.Asset);
            }
        });

        _wsClient.OnConnectionStateChanged(state =>
        {
            _healthProvider.RecordConnectionState(state == WebSocketConnectionState.Connected);
            _logger.LogInformation("WebSocket connection state: {State}", state);
            return Task.CompletedTask;
        });

        _userEventClient.OnFillReceived(async fill =>
        {
            try
            {
                await _fillProcessor.ProcessFillAsync(fill, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing fill: OrderId={OrderId}", fill.OrderId);
            }
        });

        _userEventClient.OnOrderUpdateReceived(async update =>
        {
            try
            {
                await _fillProcessor.ProcessOrderUpdateAsync(update, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order update: OrderId={OrderId}", update.OrderId);
            }
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _wsClient.ConnectAsync(stoppingToken);
                await _wsClient.SubscribeToTradesAsync(coin, stoppingToken);

                _logger.LogInformation("WebSocket connected. Subscribed to {Coin} trade stream.", coin);

                // Connect user event WebSocket for fill detection
                Task? userEventTask = null;
                if (_signerProvider.IsConfigured)
                {
                    await _userEventClient.ConnectAsync(stoppingToken);
                    await _userEventClient.SubscribeToUserEventsAsync(
                        _signerProvider.WalletAddress, stoppingToken);

                    _logger.LogInformation(
                        "User event WebSocket connected. Subscribed to fills for {Wallet}.",
                        _signerProvider.WalletAddress);

                    userEventTask = _userEventClient.ReceiveLoopAsync(stoppingToken);
                }

                _retryCount = 0;

                var marketDataTask = _wsClient.ReceiveLoopAsync(stoppingToken);

                // Run both receive loops concurrently; when either exits, we reconnect both
                if (userEventTask is not null)
                {
                    await Task.WhenAny(marketDataTask, userEventTask);
                }
                else
                {
                    await marketDataTask;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket connection error for {Coin}", coin);
            }

            if (stoppingToken.IsCancellationRequested) break;

            _retryCount++;
            if (_retryCount > MaxRetryAttempts)
            {
                _logger.LogCritical(
                    "Max reconnection attempts ({MaxRetries}) exhausted. TradingSession stopping.",
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

        _logger.LogInformation("TradingSession loop exited.");
    }

    public async ValueTask DisposeAsync()
    {
        if (IsRunning) await StopAsync();
        _cts?.Dispose();
    }
}
