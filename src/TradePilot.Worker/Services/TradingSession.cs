using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.Scheduling;
using TradePilot.Application.Scheduling.Models;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Domain.ValueObjects;
using TradePilot.Infrastructure.Hyperliquid;
using TradePilot.Infrastructure.Binance;

namespace TradePilot.Worker.Services;

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
    private const long AllowedTradeTimestampSkewMs = 30_000;
    private static readonly TimeSpan CandleFlushInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RestCandleSyncInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan AccountPollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    private readonly Exchange _exchange;
    private readonly IHyperliquidWebSocketClient? _wsClient;
    private readonly IHyperliquidUserEventClient? _userEventClient;
    private readonly IHyperliquidRestClient? _restClient;
    private readonly IExchangeHistoricalDataClient _historicalDataClient;
    private readonly IExchangeAccountClient _accountClient;
    private readonly IExchangeSymbolMapper _symbolMapper;
    private readonly CandleBuilder _candleBuilder;
    private readonly CandleClock _candleClock;
    private readonly IMarketContextBuilder _contextBuilder;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IGridController _gridController;
    private readonly IRiskEngine _riskEngine;
    private readonly IPositionManager _positionManager;
    private readonly ISignalController _signalController;
    private readonly IDcaController? _dcaController;
    private readonly IExecutionEngine _executionEngine;
    private readonly IFillProcessor _fillProcessor;
    private readonly ISignerProvider _signerProvider;
    private readonly IStateRecoveryService? _stateRecoveryService;
    private readonly IOrderTracker _orderTracker;
    private readonly IServiceScope? _serviceScope;
    private readonly ITradingHealthProvider _healthProvider;
    private readonly ITriggerOrderManager? _triggerOrderManager;
    private readonly IExecutionLogger _executionLogger;
    private readonly ILogger _logger;
    private readonly IReadOnlyList<DrawdownTier> _drawdownTiers;

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private int _retryCount;
    private int _staleTradeDropCount;
    private Func<CandleClosedEvent, Task>? _candleClosedHandler;
    private Func<FillEventDto, Task>? _fillHandler;
    private Func<OrderUpdateDto, Task>? _orderUpdateHandler;

    public StrategyConfig StrategyConfig { get; }
    public GridState GridState { get; }
    public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;
    public bool IsRunning => _runTask is not null && !_runTask.IsCompleted;

    public TradingSession(
        StrategyConfig strategyConfig,
        Exchange exchange,
        IHyperliquidWebSocketClient? wsClient,
        IHyperliquidUserEventClient? userEventClient,
        IHyperliquidRestClient? restClient,
        IExchangeHistoricalDataClient historicalDataClient,
        IExchangeAccountClient accountClient,
        IExchangeSymbolMapper symbolMapper,
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
        IOrderTracker? orderTracker = null,
        IServiceScope? serviceScope = null,
        ITriggerOrderManager? triggerOrderManager = null,
        IOptions<RiskLimitsConfig>? riskLimits = null,
        IExecutionLogger? executionLogger = null,
        IDcaController? dcaController = null)
    {
        StrategyConfig = strategyConfig;
        _exchange = exchange;
        _wsClient = wsClient;
        _userEventClient = userEventClient;
        _restClient = restClient;
        _historicalDataClient = historicalDataClient;
        _accountClient = accountClient;
        _symbolMapper = symbolMapper;
        _candleBuilder = candleBuilder;
        _candleClock = candleClock;
        _contextBuilder = contextBuilder;
        _strategyEngine = strategyEngine;
        _gridController = gridController;
        _riskEngine = riskEngine;
        _positionManager = positionManager;
        _signalController = signalController;
        _dcaController = dcaController;
        _executionEngine = executionEngine;
        _fillProcessor = fillProcessor;
        _signerProvider = signerProvider;
        _stateRecoveryService = stateRecoveryService;
        _orderTracker = orderTracker!;
        _serviceScope = serviceScope;
        _healthProvider = healthProvider;
        _triggerOrderManager = triggerOrderManager;
        _executionLogger = executionLogger ?? NullExecutionLogger.Instance;
        _logger = logger;
        GridState = gridState ?? new GridState();
        _drawdownTiers = riskLimits?.Value.DrawdownTiers ?? [];

        if (_exchange == Exchange.Hyperliquid && (_wsClient is null || _userEventClient is null || _restClient is null))
        {
            throw new InvalidOperationException("Hyperliquid trading sessions require Hyperliquid runtime dependencies.");
        }
    }

    public void Start()
    {
        if (IsRunning) return;

        _healthProvider.RecordTradingSessionStarted();
        _cts = new CancellationTokenSource();
        _runTask = RunAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts is null || _runTask is null) return;

        _logger.LogInformation("TradingSession stop requested. Cancelling open orders...");

        // Unsubscribe from CandleClock to prevent stale handler accumulation
        if (_candleClosedHandler is not null)
        {
            _candleClock.CandleClosed -= _candleClosedHandler;
            _candleClosedHandler = null;
        }

        // Clear stale tracked orders from the singleton tracker
        _orderTracker?.Clear();

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
            // Cancel exchange-native protection triggers first
            if (_triggerOrderManager is not null && GridState.ProtectionOrders.HasAny)
            {
                await _triggerOrderManager.CancelProtectionOrdersAsync(
                    GridState.ProtectionOrders, timeoutCts.Token);
                _logger.LogInformation("Protection trigger orders cancelled for {Symbol}.", StrategyConfig.Market);
            }

            await _executionEngine.CancelAllOrdersAsync(StrategyConfig.Market, timeoutCts.Token);
            _logger.LogInformation("All open orders cancelled for {Symbol}.", StrategyConfig.Market);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling orders during session stop.");
        }

        // Disconnect WebSocket
        if (_exchange == Exchange.Hyperliquid && _wsClient is not null)
        {
            try
            {
                await _wsClient.DisconnectAsync(timeoutCts.Token);
                _logger.LogInformation("WebSocket disconnected.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting WebSocket during session stop.");
            }
        }

        UnregisterUserEventHandlers();
        _healthProvider.RecordTradingSessionStopped();

        _logger.LogInformation("TradingSession stopped.");
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var triggerTimeframe = ResolveTriggerTimeframe(StrategyConfig);
        var sessionStartMs = StartedAtUtc.ToUnixTimeMilliseconds();
        var marketPair = _symbolMapper.FromExchangeSymbol(StrategyConfig.Market);
        using var candleFlushCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var restSyncIntervals = new[] { triggerTimeframe, "1h", "4h" }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _candleBuilder.Reset();
        _candleClock.Reset();
        _contextBuilder.Reset();

        var candleFlushTask = RunCandleFlushLoopAsync(candleFlushCts.Token);
        var restCandleSyncTask = RunRestCandleSyncLoopAsync(restSyncIntervals, sessionStartMs, candleFlushCts.Token);
        Task? accountPollingTask = null;

        try
        {
            _logger.LogInformation(
                "TradingSession starting: Strategy={Strategy}, Exchange={Exchange}, Market={Market}, Timeframe={Timeframe}",
                StrategyConfig.StrategyName, _exchange, StrategyConfig.Market, triggerTimeframe);

            // Attempt state recovery from DB + exchange state.
            if (_stateRecoveryService is not null && _signerProvider.IsConfigured && _orderTracker is not null)
            {
                try
                {
                    var recoveredState = await _stateRecoveryService.RecoverAsync(
                        StrategyConfig.StrategyName,
                        StrategyConfig.Market,
                        ResolveExchangeWalletAddress() ?? string.Empty,
                        _orderTracker,
                        stoppingToken);

                    // Copy recovered values into our shared GridState
                    GridState.Lifecycle = recoveredState.Lifecycle;
                    GridState.GridCycleId = recoveredState.GridCycleId;
                    GridState.FilledLevels = recoveredState.FilledLevels;
                    GridState.TotalLevels = recoveredState.TotalLevels;
                    GridState.TrailingStopHighWatermark = recoveredState.TrailingStopHighWatermark;
                    GridState.CandlesSinceEntry = recoveredState.CandlesSinceEntry;

                    // Recover protection order state (exchange-native TP/SL triggers)
                    if (recoveredState.ProtectionOrders.HasAny)
                    {
                        GridState.ProtectionOrders.StopLossOrderId = recoveredState.ProtectionOrders.StopLossOrderId;
                        GridState.ProtectionOrders.StopLossTriggerPrice = recoveredState.ProtectionOrders.StopLossTriggerPrice;
                        GridState.ProtectionOrders.TakeProfitOrderId = recoveredState.ProtectionOrders.TakeProfitOrderId;
                        GridState.ProtectionOrders.TakeProfitTriggerPrice = recoveredState.ProtectionOrders.TakeProfitTriggerPrice;
                    }

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

            // Query initial account equity from the exchange.
            var initialCapital = await ResolveInitialEquityAsync(stoppingToken);

            var scheduler = new StrategyScheduler(
                _contextBuilder,
                _strategyEngine,
                _gridController,
                _riskEngine,
                _positionManager,
                StrategyConfig,
                triggerTimeframe,
                signalController: _signalController,
                dcaController: _dcaController,
                executionLogger: _executionLogger,
                initialCapital: initialCapital,
                gridState: GridState,
                drawdownTiers: _drawdownTiers);

        // Wire fill callback to update PositionState on the scheduler
        if (_fillProcessor is FillProcessor concreteProcessor)
        {
            concreteProcessor.OnFillProcessed = async fill =>
            {
                try
                {
                    var positionState = await QueryPositionStateAsync(
                        StrategyConfig.Market, stoppingToken);
                    scheduler.UpdateState(GridState, positionState);

                    // Place or update exchange-native protection orders after position state refresh
                    if (_triggerOrderManager is not null && positionState.IsOpen
                        && GridState.Lifecycle is not GridLifecycle.Closing)
                    {
                        var protectionState = GridState.ProtectionOrders;
                        var lastContext = scheduler.LastContext;
                        if (lastContext is not null)
                        {
                            if (!protectionState.HasAny)
                            {
                                await _triggerOrderManager.PlaceProtectionOrdersAsync(
                                    positionState, StrategyConfig.Exit, lastContext,
                                    protectionState, stoppingToken);
                            }
                            else
                            {
                                await _triggerOrderManager.UpdateProtectionOrdersAsync(
                                    positionState, StrategyConfig.Exit, lastContext,
                                    protectionState, stoppingToken);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to update PositionState after fill: OrderId={OrderId}",
                        fill.OrderId);
                }
            };
        }

        _candleClosedHandler = async evt =>
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

                    // Update exchange-native protection orders on every candle close
                    // so trailing stops ratchet up with price movement
                    if (_triggerOrderManager is not null
                        && GridState.ProtectionOrders.HasAny
                        && GridState.Lifecycle is not (GridLifecycle.Closing or GridLifecycle.Closed))
                    {
                        var lastContext = scheduler.LastContext;
                        if (lastContext is not null)
                        {
                            var positionState = await QueryPositionStateAsync(
                                StrategyConfig.Market, stoppingToken);
                            if (positionState.IsOpen)
                            {
                                await _triggerOrderManager.UpdateProtectionOrdersAsync(
                                    positionState, StrategyConfig.Exit, lastContext,
                                    GridState.ProtectionOrders, stoppingToken);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing candle close: Symbol={Symbol}, Timeframe={Timeframe}",
                    evt.Symbol, evt.Timeframe);
            }
        };

        _candleClock.CandleClosed += _candleClosedHandler;

            if (_exchange == Exchange.Hyperliquid)
            {
                var coin = HyperliquidAssetMapper.ToCoin(StrategyConfig.Market);

                _wsClient!.OnTradeReceived(async trade =>
                {
                    try
                    {
                        if (!IsTradeTickCurrent(trade.TimestampMs, sessionStartMs))
                        {
                            _staleTradeDropCount++;
                            if (_staleTradeDropCount <= 5 || _staleTradeDropCount % 100 == 0)
                            {
                                _logger.LogWarning(
                                    "Ignoring stale trade tick for {Asset}: TradeTimestamp={TradeTimestamp}, SessionStart={SessionStart}, DroppedCount={DroppedCount}",
                                    trade.Asset,
                                    trade.TimestampMs,
                                    sessionStartMs,
                                    _staleTradeDropCount);
                            }

                            return;
                        }

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

                _fillHandler = async fill =>
                {
                    try
                    {
                        await _fillProcessor.ProcessFillAsync(fill, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing fill: OrderId={OrderId}", fill.OrderId);
                    }
                };

                _orderUpdateHandler = async update =>
                {
                    try
                    {
                        await _fillProcessor.ProcessOrderUpdateAsync(update, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing order update: OrderId={OrderId}", update.OrderId);
                    }
                };

                _userEventClient!.OnFillReceived(_fillHandler);
                _userEventClient.OnOrderUpdateReceived(_orderUpdateHandler);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await _wsClient.ConnectAsync(stoppingToken);
                        await _wsClient.SubscribeToTradesAsync(coin, stoppingToken);

                        _logger.LogInformation("WebSocket connected. Subscribed to {Coin} trade stream.", coin);

                        _retryCount = 0;

                        var marketDataTask = _wsClient.ReceiveLoopAsync(stoppingToken);
                        await marketDataTask;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "WebSocket connection error for {Coin}", coin);
                    }

                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    _retryCount++;
                    if (_retryCount > MaxRetryAttempts)
                    {
                        _logger.LogCritical(
                            "Max reconnection attempts ({MaxRetries}) exhausted. TradingSession stopping.",
                            MaxRetryAttempts);
                        break;
                    }

                    var backoffMs = (int)Math.Min(
                        MaxBackoffMs,
                        InitialBackoffMs * Math.Pow(2, Math.Min(_retryCount - 1, 20)));

                    _logger.LogWarning(
                        "WebSocket disconnected. Reconnecting in {BackoffMs}ms (attempt {RetryCount}/{MaxRetries})",
                        backoffMs, _retryCount, MaxRetryAttempts);

                    await Task.Delay(backoffMs, stoppingToken);
                }
            }
            else
            {
                _healthProvider.RecordConnectionState(true);
                accountPollingTask = RunAccountPollingLoopAsync(marketPair, stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
        }
        finally
        {
            await candleFlushCts.CancelAsync();

            try
            {
                await candleFlushTask;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested || candleFlushCts.IsCancellationRequested)
            {
            }

            try
            {
                await restCandleSyncTask;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested || candleFlushCts.IsCancellationRequested)
            {
            }

            if (accountPollingTask is not null)
            {
                try
                {
                    await accountPollingTask;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested || candleFlushCts.IsCancellationRequested)
                {
                }
            }

            _healthProvider.RecordTradingSessionStopped();
            _logger.LogInformation("TradingSession loop exited.");
        }
    }

    private async Task RunCandleFlushLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(CandleFlushInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await _candleBuilder.FlushClosedCandlesAsync(DateTimeOffset.UtcNow, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing closed candles for {Market}", StrategyConfig.Market);
            }
        }
    }

    private async Task RunRestCandleSyncLoopAsync(
        IReadOnlyList<string> intervals,
        long sessionStartMs,
        CancellationToken cancellationToken)
    {
        var lastSyncedBuckets = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var timer = new PeriodicTimer(RestCandleSyncInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await SyncClosedCandlesFromRestAsync(intervals, sessionStartMs, lastSyncedBuckets, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing closed candles from REST for {Market}", StrategyConfig.Market);
            }
        }
    }

    private async Task SyncClosedCandlesFromRestAsync(
        IReadOnlyList<string> intervals,
        long sessionStartMs,
        IDictionary<string, long> lastSyncedBuckets,
        CancellationToken cancellationToken)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var pair = _symbolMapper.FromExchangeSymbol(StrategyConfig.Market);

        foreach (var interval in intervals)
        {
            var intervalMs = GetIntervalMs(interval);
            var latestClosedBucket = GetLatestEligibleClosedBucketOpenTime(nowMs, sessionStartMs, intervalMs);
            if (latestClosedBucket is null)
            {
                continue;
            }

            if (lastSyncedBuckets.TryGetValue(interval, out var lastSyncedBucket) &&
                lastSyncedBucket >= latestClosedBucket.Value)
            {
                continue;
            }

            var closeTime = latestClosedBucket.Value + intervalMs;
            var snapshots = await _historicalDataClient.GetCandleSnapshotsAsync(
                pair,
                interval,
                latestClosedBucket.Value,
                closeTime,
                cancellationToken);

            var snapshot = snapshots
                .FirstOrDefault(candle => candle.Timestamp == latestClosedBucket.Value);

            if (snapshot is null)
            {
                continue;
            }

            var candle = Candle.Create(
                _exchange.ToString(),
                StrategyConfig.Market,
                interval,
                snapshot.Timestamp,
                snapshot.Open,
                snapshot.High,
                snapshot.Low,
                snapshot.Close,
                snapshot.Volume,
                snapshot.NumTrades);

            await _candleClock.ProcessCandleAsync(candle);
            lastSyncedBuckets[interval] = latestClosedBucket.Value;
        }
    }

    private async Task RunAccountPollingLoopAsync(
        TradingPair pair,
        CancellationToken cancellationToken)
    {
        var processedFillKeys = new HashSet<string>(StringComparer.Ordinal);
        var knownOpenOrderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var timer = new PeriodicTimer(AccountPollInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await PollAccountStateAsync(pair, processedFillKeys, knownOpenOrderIds, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling account state for {Exchange}/{Market}", _exchange, StrategyConfig.Market);
            }
        }
    }

    private async Task PollAccountStateAsync(
        TradingPair pair,
        ISet<string> processedFillKeys,
        ISet<string> knownOpenOrderIds,
        CancellationToken cancellationToken)
    {
        var walletAddress = ResolveExchangeWalletAddress();
        var fillsTask = _accountClient.GetRecentFillsAsync(pair, walletAddress, cancellationToken);
        var openOrdersTask = _accountClient.GetOpenOrdersAsync(walletAddress, cancellationToken);

        await Task.WhenAll(fillsTask, openOrdersTask);

        foreach (var fill in fillsTask.Result.OrderBy(fill => fill.Timestamp))
        {
            if (fill.Timestamp < StartedAtUtc.UtcDateTime)
            {
                continue;
            }

            var fillKey = BuildFillKey(fill);
            if (!processedFillKeys.Add(fillKey))
            {
                continue;
            }

            await _fillProcessor.ProcessFillAsync(fill, cancellationToken);
        }

        var currentOpenOrderIds = openOrdersTask.Result
            .Select(order => order.OrderId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var cancelledOrderId in knownOpenOrderIds.Except(currentOpenOrderIds, StringComparer.OrdinalIgnoreCase).ToArray())
        {
            var trackedOrder = _orderTracker.GetOrder(cancelledOrderId);
            if (trackedOrder?.Status == TrackedOrderStatus.Filled)
            {
                knownOpenOrderIds.Remove(cancelledOrderId);
                continue;
            }

            await _fillProcessor.ProcessOrderUpdateAsync(
                new OrderUpdateDto
                {
                    Timestamp = DateTime.UtcNow,
                    OrderId = cancelledOrderId,
                    Asset = StrategyConfig.Market,
                    Status = "cancelled",
                },
                cancellationToken);

            knownOpenOrderIds.Remove(cancelledOrderId);
        }

        foreach (var orderId in currentOpenOrderIds)
        {
            knownOpenOrderIds.Add(orderId);
        }
    }

    internal static bool IsTradeTickCurrent(long tradeTimestampMs, long sessionStartMs)
    {
        return tradeTimestampMs + AllowedTradeTimestampSkewMs >= sessionStartMs;
    }

    internal static long? GetLatestEligibleClosedBucketOpenTime(long nowMs, long sessionStartMs, long intervalMs)
    {
        var latestCloseBoundary = nowMs / intervalMs * intervalMs;
        var latestClosedBucketOpen = latestCloseBoundary - intervalMs;
        if (latestClosedBucketOpen < 0)
        {
            return null;
        }

        return latestClosedBucketOpen + intervalMs >= sessionStartMs
            ? latestClosedBucketOpen
            : null;
    }

    private void UnregisterUserEventHandlers()
    {
        if (_fillHandler is not null && _userEventClient is not null)
        {
            _userEventClient.RemoveFillReceivedHandler(_fillHandler);
            _fillHandler = null;
        }

        if (_orderUpdateHandler is not null && _userEventClient is not null)
        {
            _userEventClient.RemoveOrderUpdateReceivedHandler(_orderUpdateHandler);
            _orderUpdateHandler = null;
        }
    }

    private static string ResolveTriggerTimeframe(StrategyConfig config)
    {
        if (config.StrategyMode != StrategyMode.Dca || config.Dca is null)
        {
            return config.Timeframe;
        }

        return config.Dca.Interval switch
        {
            DcaInterval.FiveMinutes => "5m",
            _ => "1h",
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (IsRunning) await StopAsync();
        _cts?.Dispose();
        _serviceScope?.Dispose();
    }

    private async Task<decimal> ResolveInitialEquityAsync(CancellationToken cancellationToken)
    {
        if (!_signerProvider.IsConfigured)
        {
            return 0m;
        }

        try
        {
            if (_executionEngine is IPositionQueryable queryable)
            {
                var equity = await queryable.QueryAccountEquityAsync(cancellationToken);
                _logger.LogInformation("Initial account equity resolved: {Equity}", equity);
                return equity;
            }

            return 0m;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query initial account equity. Using 0 as fallback.");
            return 0m;
        }
    }

    private async Task<PositionState> QueryPositionStateAsync(
        string symbol, CancellationToken cancellationToken)
    {
        // Query the exchange for current position — exchange is source of truth.
        if (_signerProvider.IsConfigured && _executionEngine is IPositionQueryable queryable)
        {
            return await queryable.QueryPositionAsync(symbol, cancellationToken);
        }

        return new PositionState { Symbol = symbol };
    }

    private string? ResolveExchangeWalletAddress()
    {
        return _exchange == Exchange.Hyperliquid && _signerProvider.IsConfigured
            ? _signerProvider.WalletAddress
            : null;
    }

    private long GetIntervalMs(string interval)
    {
        return _exchange == Exchange.Binance
            ? BinanceAssetMapper.GetIntervalMs(interval)
            : HyperliquidAssetMapper.GetIntervalMs(interval);
    }

    private static string BuildFillKey(FillEventDto fill)
    {
        return string.Join(
            ':',
            fill.OrderId,
            fill.Timestamp.Ticks.ToString(),
            fill.Price.ToString(System.Globalization.CultureInfo.InvariantCulture),
            fill.Size.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
