using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Agent.Models;
using TradingApp.Application.Scheduling;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Domain.Enums;

namespace TradingApp.Worker.Services;

/// <summary>
/// Background service that periodically checks in with the API control plane.
/// Sends heartbeats, picks up pending commands (Start/Stop/PlaceOrder/Cancel),
/// and reports order results back.
/// </summary>
public sealed class AgentCheckInService : BackgroundService
{
    private static readonly TimeSpan CheckInInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(15);

    /// <summary>
    /// JSON options matching the API's serialization config (camelCase properties, snake_case enums).
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    internal const string HttpClientName = "ControlPlane";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISignerProvider _signerProvider;
    private readonly ITradingHealthProvider _healthProvider;
    private readonly IUpdateNotifier _updateNotifier;
    private readonly AgentOptions _agentOptions;
    private readonly ILogger<AgentCheckInService> _logger;

    private TradingSession? _activeSession;
    private readonly object _sessionLock = new();

    /// <summary>
    /// Completed order results waiting to be sent back on the next heartbeat.
    /// </summary>
    private readonly ConcurrentQueue<OrderCommandResult> _pendingResults = new();

    /// <summary>Agent executable version, read once at startup.</summary>
    private static readonly string AgentVersion = GetAgentVersion();

    public AgentCheckInService(
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ISignerProvider signerProvider,
        ITradingHealthProvider healthProvider,
        IUpdateNotifier updateNotifier,
        IOptions<AgentOptions> agentOptions,
        ILogger<AgentCheckInService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _signerProvider = signerProvider;
        _healthProvider = healthProvider;
        _updateNotifier = updateNotifier;
        _agentOptions = agentOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AgentCheckInService started. AgentId={AgentId}, ControlPlane={Url}",
            _agentOptions.AgentId, _agentOptions.ControlPlaneUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckInAsync(stoppingToken);
                await Task.Delay(CheckInInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Check-in failed. Retrying in {Seconds}s.", ErrorBackoff.TotalSeconds);

                try
                {
                    await Task.Delay(ErrorBackoff, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        // Graceful shutdown — stop any active session
        if (_activeSession is not null)
        {
            _logger.LogInformation("Host shutting down. Stopping active trading session...");
            await _activeSession.StopAsync();
            await _activeSession.DisposeAsync();
            _activeSession = null;
        }

        _logger.LogInformation("AgentCheckInService stopped.");
    }

    private async Task CheckInAsync(CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var heartbeat = BuildHeartbeat();

        var response = await httpClient.PostAsJsonAsync(
            "/api/agent/heartbeat", heartbeat, JsonOptions, cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(
            JsonOptions, cancellationToken: cancellationToken);

        if (result is null) return;

        // Kill switch — stop everything and halt the heartbeat loop
        if (result.MustShutdown)
        {
            _logger.LogCritical(
                "Kill switch activated by control plane: {Reason}. Shutting down.",
                result.ShutdownReason ?? "No reason given.");

            if (_activeSession is not null)
            {
                _logger.LogInformation("Stopping active session due to kill switch...");
                await _activeSession.StopAsync();
                await _activeSession.DisposeAsync();

                lock (_sessionLock)
                {
                    _activeSession = null;
                }
            }

            // Keep heartbeating (so the control plane can reinstate us)
            // but don't process any commands
            return;
        }

        if (result.PendingCommands is { Count: > 0 })
        {
            _logger.LogInformation(
                "Received {Count} command(s) from control plane.", result.PendingCommands.Count);
            foreach (var command in result.PendingCommands)
            {
                await HandleCommandAsync(command, cancellationToken);
            }
        }

        // Forward update notification to UpdateCheckerService
        if (result.UpdateAvailable &&
            !string.IsNullOrEmpty(result.LatestVersion) &&
            !string.IsNullOrEmpty(result.UpdateDownloadUrl) &&
            !string.IsNullOrEmpty(result.UpdateSha256Hash))
        {
            _updateNotifier.NotifyUpdateAvailable(
                result.LatestVersion, result.UpdateDownloadUrl, result.UpdateSha256Hash);
        }
    }

    private AgentHeartbeat BuildHeartbeat()
    {
        ActiveStrategyInfo? activeStrategy = null;
        AgentState state;

        lock (_sessionLock)
        {
            if (_activeSession is { IsRunning: true })
            {
                state = AgentState.Running;
                activeStrategy = new ActiveStrategyInfo
                {
                    StrategyName = _activeSession.StrategyConfig.StrategyName,
                    Market = _activeSession.StrategyConfig.Market,
                    Timeframe = _activeSession.StrategyConfig.Timeframe,
                    StartedAtUtc = _activeSession.StartedAtUtc,
                };
            }
            else
            {
                state = AgentState.Idle;
            }
        }

        // Drain completed order results to send back with this heartbeat
        var orderResults = new List<OrderCommandResult>();
        while (_pendingResults.TryDequeue(out var r))
        {
            orderResults.Add(r);
        }

        // Get update state from UpdateCheckerService
        var updateChecker = _updateNotifier as UpdateCheckerService;

        return new AgentHeartbeat
        {
            AgentId = _agentOptions.AgentId,
            State = state,
            MachineName = Environment.MachineName,
            WalletAddress = _signerProvider.IsConfigured ? _signerProvider.WalletAddress : null,
            ActiveStrategy = activeStrategy,
            TimestampUtc = DateTimeOffset.UtcNow,
            OrderResults = orderResults,
            AgentVersion = AgentVersion,
            UpdateState = updateChecker?.CurrentState ?? UpdateState.None,
            UpdateDeferredReason = updateChecker?.DeferredReason,
        };
    }

    private async Task HandleCommandAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received command: Type={Type}, CommandId={CommandId}",
            command.Type, command.CommandId);

        switch (command.Type)
        {
            case AgentCommandType.Start:
                await HandleStartAsync(command, cancellationToken);
                break;

            case AgentCommandType.Stop:
                await HandleStopAsync();
                break;

            case AgentCommandType.PlaceOrder:
                await HandlePlaceOrderAsync(command, cancellationToken);
                break;

            case AgentCommandType.CancelOrder:
                await HandleCancelOrderAsync(command, cancellationToken);
                break;

            case AgentCommandType.CancelAllOrders:
                await HandleCancelAllOrdersAsync(command, cancellationToken);
                break;

            case AgentCommandType.SetLeverage:
                await HandleSetLeverageAsync(command, cancellationToken);
                break;

            case AgentCommandType.PlaceTriggerOrder:
                await HandlePlaceTriggerOrderAsync(command, cancellationToken);
                break;

            case AgentCommandType.ModifyTriggerOrder:
                await HandleModifyTriggerOrderAsync(command, cancellationToken);
                break;

            case AgentCommandType.CancelTriggerOrder:
                await HandleCancelTriggerOrderAsync(command, cancellationToken);
                break;

            default:
                _logger.LogWarning("Unknown command type: {Type}", command.Type);
                break;
        }
    }

    private async Task HandleStartAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        if (command.StrategyConfig is null)
        {
            _logger.LogError("Start command received without StrategyConfig. Ignoring.");
            return;
        }

        if (!_signerProvider.IsConfigured)
        {
            _logger.LogError("Cannot start trading — wallet not configured.");
            return;
        }

        // Stop any existing session first
        if (_activeSession is not null)
        {
            _logger.LogInformation("Stopping existing session before starting new one.");
            await _activeSession.StopAsync();
            await _activeSession.DisposeAsync();
        }

        var session = CreateSession(command.StrategyConfig);

        lock (_sessionLock)
        {
            _activeSession = session;
        }

        session.Start();

        _logger.LogInformation(
            "Trading session started: Strategy={Strategy}, Market={Market}",
            command.StrategyConfig.StrategyName, command.StrategyConfig.Market);
    }

    private async Task HandleStopAsync()
    {
        TradingSession? session;
        lock (_sessionLock)
        {
            session = _activeSession;
            _activeSession = null;
        }

        if (session is null)
        {
            _logger.LogWarning("Stop command received but no active session.");
            return;
        }

        await session.StopAsync();
        await session.DisposeAsync();

        _logger.LogInformation("Trading session stopped by dashboard command.");
    }

    private async Task HandlePlaceOrderAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        if (command.OrderPayload is null)
        {
            _logger.LogError("PlaceOrder command missing OrderPayload. CommandId={CommandId}", command.CommandId);
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Missing order payload."
            });
            return;
        }

        if (!_signerProvider.IsConfigured)
        {
            _logger.LogError("Cannot place order — wallet not configured.");
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Wallet not configured on agent."
            });
            return;
        }

        var payload = command.OrderPayload;
        var executionEngine = _serviceProvider.GetRequiredService<IExecutionEngine>();

        try
        {
            var orderRequest = new OrderRequest
            {
                Symbol = payload.Asset,
                Side = payload.Side.Equals("buy", StringComparison.OrdinalIgnoreCase) ? OrderSide.Buy : OrderSide.Sell,
                OrderType = payload.OrderType.Equals("market", StringComparison.OrdinalIgnoreCase) ? OrderType.Market : OrderType.Limit,
                Price = payload.Price ?? 0m,
                Size = payload.Size,
                TradeType = TradeType.Manual,
            };

            var orderId = await executionEngine.PlaceOrderAsync(orderRequest, cancellationToken);

            var success = !string.IsNullOrEmpty(orderId);
            _logger.LogInformation(
                "Order command completed: CommandId={CommandId}, Success={Success}, OrderId={OrderId}",
                command.CommandId, success, orderId);

            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = success,
                OrderId = orderId,
                Detail = success ? null : "Order rejected by exchange."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order execution failed: CommandId={CommandId}", command.CommandId);
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = ex.Message
            });
        }
    }

    private async Task HandleCancelOrderAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        if (command.CancelPayload is null)
        {
            _logger.LogError("CancelOrder command missing CancelPayload. CommandId={CommandId}", command.CommandId);
            return;
        }

        var executionEngine = _serviceProvider.GetRequiredService<IExecutionEngine>();

        try
        {
            await executionEngine.CancelOrderAsync(command.CancelPayload.OrderId, cancellationToken);
            _logger.LogInformation("Order cancelled: OrderId={OrderId}", command.CancelPayload.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cancel order failed: OrderId={OrderId}", command.CancelPayload.OrderId);
        }
    }

    private async Task HandleCancelAllOrdersAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        if (command.CancelAllPayload is null)
        {
            _logger.LogError("CancelAllOrders command missing payload. CommandId={CommandId}", command.CommandId);
            return;
        }

        var executionEngine = _serviceProvider.GetRequiredService<IExecutionEngine>();

        try
        {
            await executionEngine.CancelAllOrdersAsync(command.CancelAllPayload.Asset, cancellationToken);
            _logger.LogInformation("All orders cancelled: Asset={Asset}", command.CancelAllPayload.Asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cancel all orders failed: Asset={Asset}", command.CancelAllPayload.Asset);
        }
    }

    private async Task HandleSetLeverageAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        if (command.LeveragePayload is null)
        {
            _logger.LogError("SetLeverage command missing payload. CommandId={CommandId}", command.CommandId);
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Missing leverage payload."
            });
            return;
        }

        if (!_signerProvider.IsConfigured)
        {
            _logger.LogError("Cannot set leverage — wallet not configured.");
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Wallet not configured on agent."
            });
            return;
        }

        var payload = command.LeveragePayload;
        var executionEngine = _serviceProvider.GetRequiredService<IExecutionEngine>();

        try
        {
            await executionEngine.SetLeverageAsync(
                payload.Asset,
                payload.Leverage,
                isIsolated: !payload.IsCross,
                cancellationToken);

            _logger.LogInformation(
                "SetLeverage executed. Asset={Asset}, Leverage={Leverage}x, IsCross={IsCross}",
                payload.Asset,
                payload.Leverage,
                payload.IsCross);

            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SetLeverage failed: CommandId={CommandId}", command.CommandId);
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = ex.Message
            });
        }
    }

    private async Task HandlePlaceTriggerOrderAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        if (command.TriggerPayload is null)
        {
            _logger.LogError("PlaceTriggerOrder command missing TriggerPayload. CommandId={CommandId}", command.CommandId);
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Missing trigger order payload."
            });
            return;
        }

        if (!_signerProvider.IsConfigured)
        {
            _logger.LogError("Cannot place trigger order — wallet not configured.");
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Wallet not configured on agent."
            });
            return;
        }

        var payload = command.TriggerPayload;
        var executionEngine = _serviceProvider.GetRequiredService<IExecutionEngine>();

        try
        {
            var orderId = await executionEngine.PlaceTriggerOrderAsync(
                payload.Asset, payload.Side, payload.Size, payload.TriggerPrice, payload.TpslType, cancellationToken);

            var success = !string.IsNullOrEmpty(orderId);
            _logger.LogInformation(
                "Trigger order command completed: CommandId={CommandId}, Success={Success}, OrderId={OrderId}",
                command.CommandId, success, orderId);

            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = success,
                OrderId = orderId,
                Detail = success ? null : "Trigger order rejected by exchange."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trigger order execution failed: CommandId={CommandId}", command.CommandId);
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = ex.Message
            });
        }
    }

    private async Task HandleModifyTriggerOrderAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        if (command.ModifyTriggerPayload is null)
        {
            _logger.LogError("ModifyTriggerOrder command missing payload. CommandId={CommandId}", command.CommandId);
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Missing modify trigger order payload."
            });
            return;
        }

        if (!_signerProvider.IsConfigured)
        {
            _logger.LogError("Cannot modify trigger order — wallet not configured.");
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Wallet not configured on agent."
            });
            return;
        }

        var payload = command.ModifyTriggerPayload;
        var executionEngine = _serviceProvider.GetRequiredService<IExecutionEngine>();

        try
        {
            await executionEngine.ModifyTriggerOrderAsync(
                payload.OrderId, payload.Asset, payload.Side, payload.TriggerPrice, payload.Size, payload.TpslType, cancellationToken);

            _logger.LogInformation(
                "Modify trigger order completed: CommandId={CommandId}, OrderId={OrderId}",
                command.CommandId, payload.OrderId);

            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = true,
                OrderId = payload.OrderId,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Modify trigger order failed: CommandId={CommandId}", command.CommandId);
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = ex.Message
            });
        }
    }

    private async Task HandleCancelTriggerOrderAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        if (command.CancelPayload is null)
        {
            _logger.LogError("CancelTriggerOrder command missing CancelPayload. CommandId={CommandId}", command.CommandId);
            return;
        }

        var executionEngine = _serviceProvider.GetRequiredService<IExecutionEngine>();

        try
        {
            // Trigger orders use the same cancel mechanism as regular orders
            await executionEngine.CancelOrderAsync(command.CancelPayload.OrderId, cancellationToken);
            _logger.LogInformation("Trigger order cancelled: OrderId={OrderId}", command.CancelPayload.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cancel trigger order failed: OrderId={OrderId}", command.CancelPayload.OrderId);
        }
    }

    private TradingSession CreateSession(StrategyConfig strategyConfig)
    {
        var gridState = new GridState();
        var orderTracker = _serviceProvider.GetRequiredService<IOrderTracker>();
        var riskEngine = _serviceProvider.GetRequiredService<IRiskEngine>();

        // Create a scope for scoped repository services — owned by TradingSession
        var scope = _serviceProvider.CreateScope();
        var userId = _signerProvider.IsConfigured ? _signerProvider.WalletAddress : null;

        var fillProcessor = new FillProcessor(
            orderTracker,
            gridState,
            _serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<FillProcessor>(),
            riskEngine,
            scope.ServiceProvider.GetService<ILiveOrderRepository>(),
            scope.ServiceProvider.GetService<ILiveFillRepository>(),
            scope.ServiceProvider.GetService<IGridCycleRepository>(),
            userId,
            _serviceProvider.GetRequiredService<IExecutionEngine>());

        // Wire scoped repositories into the singleton LivePositionManager
        var positionManager = _serviceProvider.GetRequiredService<IPositionManager>();
        if (positionManager is LivePositionManager livePositionManager)
        {
            livePositionManager.ConfigureRepositories(
                scope.ServiceProvider.GetService<IGridCycleRepository>(),
                scope.ServiceProvider.GetService<ILiveOrderRepository>(),
                userId);
            livePositionManager.ConfigureProtectionState(gridState.ProtectionOrders);
        }

        var triggerOrderManager = _serviceProvider.GetService<ITriggerOrderManager>();

        return new TradingSession(
            strategyConfig,
            _serviceProvider.GetRequiredService<IHyperliquidWebSocketClient>(),
            _serviceProvider.GetRequiredService<IHyperliquidUserEventClient>(),
            _serviceProvider.GetRequiredService<CandleBuilder>(),
            _serviceProvider.GetRequiredService<CandleClock>(),
            _serviceProvider.GetRequiredService<IMarketContextBuilder>(),
            _serviceProvider.GetRequiredService<IStrategyEngine>(),
            _serviceProvider.GetRequiredService<IGridController>(),
            riskEngine,
            positionManager,
            _serviceProvider.GetRequiredService<ISignalController>(),
            _serviceProvider.GetRequiredService<IExecutionEngine>(),
            fillProcessor,
            _signerProvider,
            _healthProvider,
            _logger,
            gridState,
            scope.ServiceProvider.GetService<IStateRecoveryService>(),
            orderTracker,
            scope,
            triggerOrderManager);
    }

    private static string GetAgentVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;
        return version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
    }
}

/// <summary>
/// Configuration for the agent check-in service.
/// </summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Unique identifier for this agent instance.</summary>
    public string AgentId { get; set; } = Environment.MachineName.ToLowerInvariant();

    /// <summary>Base URL of the API control plane.</summary>
    public string ControlPlaneUrl { get; set; } = "http://localhost:5062";
}
