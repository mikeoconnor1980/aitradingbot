using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.Scheduling;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Trading;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Enums;

namespace TradePilot.Worker.Services;

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

    internal static void ConfigureControlPlaneHttpClient(HttpClient client, AgentOptions agentOptions)
    {
        client.BaseAddress = new Uri(agentOptions.ControlPlaneUrl);
        client.Timeout = TimeSpan.FromSeconds(10);

        if (string.IsNullOrWhiteSpace(agentOptions.SecretKey))
        {
            client.DefaultRequestHeaders.Authorization = null;
            return;
        }

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", agentOptions.SecretKey);
    }

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IExecutionEngineResolver _executionEngineResolver;
    private readonly ISignerProvider _signerProvider;
    private readonly ITradingHealthProvider _healthProvider;
    private readonly IUpdateNotifier _updateNotifier;
    private readonly IExecutionLogger _executionLogger;
    private readonly ITelegramNotifier _telegramNotifier;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly NotificationConfigHolder _notificationConfig;
    private readonly HyperliquidOptions _hyperliquidOptions;
    private readonly AgentOptions _agentOptions;
    private readonly ILogger<AgentCheckInService> _logger;
    private readonly object _networkConfigLock = new();

    private TradingSession? _activeSession;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    /// <summary>
    /// Completed order results waiting to be sent back on the next heartbeat.
    /// </summary>
    private readonly ConcurrentQueue<OrderCommandResult> _pendingResults = new();

    /// <summary>Agent executable version, read once at startup.</summary>
    private static readonly string AgentVersion = GetAgentVersion();

    public AgentCheckInService(
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        IExecutionEngineResolver executionEngineResolver,
        ISignerProvider signerProvider,
        ITradingHealthProvider healthProvider,
        IUpdateNotifier updateNotifier,
        IExecutionLogger executionLogger,
        ITelegramNotifier telegramNotifier,
        INotificationDispatcher notificationDispatcher,
        NotificationConfigHolder notificationConfig,
        IOptions<HyperliquidOptions> hyperliquidOptions,
        IOptions<AgentOptions> agentOptions,
        ILogger<AgentCheckInService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _executionEngineResolver = executionEngineResolver;
        _signerProvider = signerProvider;
        _healthProvider = healthProvider;
        _updateNotifier = updateNotifier;
        _executionLogger = executionLogger;
        _telegramNotifier = telegramNotifier;
        _notificationDispatcher = notificationDispatcher;
        _notificationConfig = notificationConfig;
        _hyperliquidOptions = hyperliquidOptions.Value;
        _agentOptions = agentOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AgentCheckInService started. AgentId={AgentId}, ControlPlane={Url}",
            _agentOptions.AgentId, _agentOptions.ControlPlaneUrl);

        _executionLogger.Log(new ExecutionLogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Category = ExecutionLogCategory.CandleClose,
            Level = ExecutionLogLevel.Summary,
            Message = $"Agent started — {Environment.MachineName} v{AgentVersion}",
            Data = new Dictionary<string, object>
            {
                ["agentId"] = _agentOptions.AgentId,
                ["machine"] = Environment.MachineName,
                ["version"] = AgentVersion,
            },
        });

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
        TradingSession? activeSession;
        await _sessionLock.WaitAsync(CancellationToken.None);
        try
        {
            activeSession = _activeSession;
            _activeSession = null;
        }
        finally
        {
            _sessionLock.Release();
        }

        if (activeSession is not null)
        {
            _logger.LogInformation("Host shutting down. Stopping active trading session...");
            await activeSession.StopAsync();
            await activeSession.DisposeAsync();
        }

        _logger.LogInformation("AgentCheckInService stopped.");
    }

    private async Task CheckInAsync(CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var heartbeat = await BuildHeartbeatAsync(cancellationToken);

        var response = await httpClient.PostAsJsonAsync(
            "/api/agent/heartbeat", heartbeat, JsonOptions, cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(
            JsonOptions, cancellationToken: cancellationToken);

        if (result is null) return;

        // Apply network config from control plane
        if (result.NetworkConfig is { } netCfg)
        {
            await ApplyNetworkConfigAsync(netCfg, cancellationToken);
        }

        // Apply notification config from control plane
        if (result.NotificationConfig is { } notifCfg)
        {
            if (_notificationConfig.TelegramChatId != notifCfg.TelegramChatId)
            {
                _logger.LogInformation(
                    "Telegram chat ID updated: {ChatId}, BotToken={HasToken}",
                    notifCfg.TelegramChatId,
                    !string.IsNullOrEmpty(notifCfg.TelegramBotToken));
            }
            _notificationConfig.TelegramChatId = notifCfg.TelegramChatId;
            _notificationConfig.TelegramBotToken = notifCfg.TelegramBotToken;
        }
        else
        {
            _logger.LogWarning(
                "Heartbeat returned NULL notification config — API may not have Telegram bot token or wallet lookup failed");
        }

        // Kill switch — stop everything and halt the heartbeat loop
        if (result.MustShutdown)
        {
            _executionLogger.Log(new ExecutionLogEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Category = ExecutionLogCategory.Signal,
                Level = ExecutionLogLevel.Summary,
                Message = $"Kill switch activated — {result.ShutdownReason ?? "No reason given"}",
            });

            _logger.LogCritical(
                "Kill switch activated by control plane: {Reason}. Shutting down.",
                result.ShutdownReason ?? "No reason given.");

            TradingSession? activeSession;
            await _sessionLock.WaitAsync(cancellationToken);
            try
            {
                activeSession = _activeSession;

                if (activeSession is not null)
                {
                    _logger.LogInformation("Stopping active session due to kill switch...");
                    await activeSession.StopAsync();
                    await activeSession.DisposeAsync();
                    _activeSession = null;
                }
            }
            finally
            {
                _sessionLock.Release();
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

    private async Task<AgentHeartbeat> BuildHeartbeatAsync(CancellationToken cancellationToken)
    {
        ActiveStrategyInfo? activeStrategy = null;
        AgentState state;
        string? lastError = null;

        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            if (_activeSession is not null)
            {
                activeStrategy = new ActiveStrategyInfo
                {
                    StrategyName = _activeSession.StrategyConfig.StrategyName,
                    Market = _activeSession.StrategyConfig.Market,
                    Timeframe = _activeSession.StrategyConfig.Timeframe,
                    StartedAtUtc = _activeSession.StartedAtUtc,
                };

                if (_activeSession.IsRunning)
                {
                    state = AgentState.Running;
                }
                else
                {
                    // Session was assigned but the run task completed (crash/disconnect)
                    state = AgentState.Error;
                    lastError = "Trading session stopped unexpectedly";
                }
            }
            else
            {
                state = AgentState.Idle;
            }
        }
        finally
        {
            _sessionLock.Release();
        }

        // Drain completed order results to send back with this heartbeat
        var orderResults = new List<OrderCommandResult>();
        while (_pendingResults.TryDequeue(out var r))
        {
            orderResults.Add(r);
        }

        // Drain execution log entries from the strategy evaluation pipeline
        var executionLogs = _executionLogger.Drain();

        // Get update state from UpdateCheckerService
        var updateChecker = _updateNotifier as UpdateCheckerService;

        return new AgentHeartbeat
        {
            AgentId = _agentOptions.AgentId,
            State = state,
            MachineName = Environment.MachineName,
            WalletAddress = _signerProvider.IsConfigured ? _signerProvider.WalletAddress : null,
            ActiveStrategy = activeStrategy,
            LastError = lastError,
            TimestampUtc = DateTimeOffset.UtcNow,
            OrderResults = orderResults,
            ExecutionLogs = executionLogs,
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
                await HandleStopAsync(cancellationToken);
                break;

            case AgentCommandType.PlaceOrder:
                await HandlePlaceOrderAsync(command, cancellationToken);
                break;

            case AgentCommandType.ClosePosition:
                await HandleClosePositionAsync(command, cancellationToken);
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
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Missing strategy configuration."
            });
            return;
        }

        if (!LiveTradingSupport.TryValidate(command.StrategyConfig, out var unsupportedReason))
        {
            _logger.LogError(
                "Cannot start strategy {StrategyName} on {Market}: {Reason}",
                command.StrategyConfig.StrategyName,
                command.StrategyConfig.Market,
                unsupportedReason);

            _executionLogger.Log(new ExecutionLogEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Category = ExecutionLogCategory.Signal,
                Level = ExecutionLogLevel.Summary,
                Message = unsupportedReason!,
                Data = new Dictionary<string, object>
                {
                    ["strategy"] = command.StrategyConfig.StrategyName,
                    ["market"] = command.StrategyConfig.Market,
                    ["timeframe"] = command.StrategyConfig.Timeframe,
                    ["mode"] = command.StrategyConfig.StrategyMode.ToString(),
                },
            });

            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = unsupportedReason,
            });

            return;
        }

        if (!_signerProvider.IsConfigured)
        {
            _logger.LogError("Cannot start trading — wallet not configured.");
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Wallet not configured on agent."
            });
            return;
        }

        TradingSession session;

        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            if (_activeSession is not null)
            {
                _logger.LogInformation("Stopping existing session before starting new one.");
                await _activeSession.StopAsync();
                await _activeSession.DisposeAsync();
                _activeSession = null;
            }

            session = CreateSession(command.StrategyConfig);
            _activeSession = session;
        }
        finally
        {
            _sessionLock.Release();
        }

        session.Start();

        _executionLogger.Log(new ExecutionLogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Category = ExecutionLogCategory.Signal,
            Level = ExecutionLogLevel.Summary,
            Message = $"Trading started — {command.StrategyConfig.StrategyName} on {command.StrategyConfig.Market} ({command.StrategyConfig.Timeframe}) [{_hyperliquidOptions.Network}]",
            Data = new Dictionary<string, object>
            {
                ["strategy"] = command.StrategyConfig.StrategyName,
                ["market"] = command.StrategyConfig.Market,
                ["timeframe"] = command.StrategyConfig.Timeframe,
                ["network"] = _hyperliquidOptions.Network,
            },
        });

        _logger.LogInformation(
            "Trading session started: Strategy={Strategy}, Market={Market}",
            command.StrategyConfig.StrategyName, command.StrategyConfig.Market);

        if (_notificationConfig.TelegramChatId is { })
        {
            await _notificationDispatcher.NotifyStrategyEventAsync(
                "started", command.StrategyConfig.StrategyName,
                $"{command.StrategyConfig.Market} ({command.StrategyConfig.Timeframe})");
        }
    }

    private async Task HandleStopAsync(CancellationToken cancellationToken)
    {
        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            if (_activeSession is null)
            {
                _logger.LogWarning("Stop command received but no active session.");
                return;
            }

            await _activeSession.StopAsync();
            await _activeSession.DisposeAsync();
            _activeSession = null;
        }
        finally
        {
            _sessionLock.Release();
        }

        _executionLogger.Log(new ExecutionLogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Category = ExecutionLogCategory.Signal,
            Level = ExecutionLogLevel.Summary,
            Message = "Trading stopped by dashboard command",
        });

        _logger.LogInformation("Trading session stopped by dashboard command.");

        if (_notificationConfig.TelegramChatId is { })
        {
            await _notificationDispatcher.NotifyStrategyEventAsync("stopped", "Trading session");
        }
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
        var executionEngine = _executionEngineResolver.Resolve(await ResolveCommandExchangeAsync(cancellationToken));

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

            await PlaceCompanionTriggerOrdersAsync(executionEngine, payload, cancellationToken);

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

    private async Task HandleClosePositionAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        if (command.ClosePositionPayload is null)
        {
            _logger.LogError("ClosePosition command missing payload. CommandId={CommandId}", command.CommandId);
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Missing close-position payload."
            });
            return;
        }

        if (!_signerProvider.IsConfigured)
        {
            _logger.LogError("Cannot close position — wallet not configured.");
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Wallet not configured on agent."
            });
            return;
        }

        var payload = command.ClosePositionPayload;
        if (payload.Amount is <= 0m)
        {
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Close amount must be positive when provided."
            });
            return;
        }

        var executionEngine = _executionEngineResolver.Resolve(await ResolveCommandExchangeAsync(cancellationToken));
        if (executionEngine is not IPositionQueryable positionQueryable)
        {
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = "Execution engine does not support live position queries."
            });
            return;
        }

        try
        {
            await executionEngine.CancelAllOrdersAsync(payload.Asset, cancellationToken);

            var position = await positionQueryable.QueryPositionAsync(payload.Asset, cancellationToken);
            var absoluteSize = Math.Abs(position.Size);

            if (absoluteSize <= 0m)
            {
                _pendingResults.Enqueue(new OrderCommandResult
                {
                    CommandId = command.CommandId,
                    Success = true,
                    Detail = "No open position found. Cleared open orders for asset."
                });
                return;
            }

            var sizeToClose = payload.Amount.HasValue
                ? Math.Min(payload.Amount.Value, absoluteSize)
                : absoluteSize;

            var orderId = await executionEngine.PlaceOrderAsync(
                new OrderRequest
                {
                    Symbol = payload.Asset,
                    Side = position.Size > 0m ? OrderSide.Sell : OrderSide.Buy,
                    OrderType = OrderType.Market,
                    Price = 0m,
                    Size = sizeToClose,
                    TradeType = TradeType.Manual,
                    ReduceOnly = true,
                },
                cancellationToken);

            var success = !string.IsNullOrWhiteSpace(orderId);
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = success,
                OrderId = orderId,
                Detail = success
                    ? $"Submitted reduce-only close for {sizeToClose} {payload.Asset}."
                    : "Close position order rejected by exchange."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Close position failed: CommandId={CommandId}, Asset={Asset}", command.CommandId, payload.Asset);
            _pendingResults.Enqueue(new OrderCommandResult
            {
                CommandId = command.CommandId,
                Success = false,
                Detail = ex.Message
            });
        }
    }

    private async Task PlaceCompanionTriggerOrdersAsync(
        IExecutionEngine executionEngine,
        OrderCommandPayload payload,
        CancellationToken cancellationToken)
    {
        if (!payload.StopLossPrice.HasValue && !payload.TakeProfitPrice.HasValue)
        {
            return;
        }

        var closingSide = payload.Side.Equals("buy", StringComparison.OrdinalIgnoreCase) ? "sell" : "buy";

        if (payload.StopLossPrice.HasValue)
        {
            await executionEngine.PlaceTriggerOrderAsync(
                payload.Asset,
                closingSide,
                payload.Size,
                payload.StopLossPrice.Value,
                "sl",
                cancellationToken);
        }

        if (payload.TakeProfitPrice.HasValue)
        {
            await executionEngine.PlaceTriggerOrderAsync(
                payload.Asset,
                closingSide,
                payload.Size,
                payload.TakeProfitPrice.Value,
                "tp",
                cancellationToken);
        }
    }

    private async Task HandleCancelOrderAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        if (command.CancelPayload is null)
        {
            _logger.LogError("CancelOrder command missing CancelPayload. CommandId={CommandId}", command.CommandId);
            return;
        }

        var executionEngine = _executionEngineResolver.Resolve(await ResolveCommandExchangeAsync(cancellationToken));

        try
        {
            await executionEngine.CancelOrderAsync(command.CancelPayload.OrderId, command.CancelPayload.Asset, cancellationToken);
            _logger.LogInformation("Order cancelled: OrderId={OrderId}, Asset={Asset}", command.CancelPayload.OrderId, command.CancelPayload.Asset);
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

        var executionEngine = _executionEngineResolver.Resolve(await ResolveCommandExchangeAsync(cancellationToken));

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
        var executionEngine = _executionEngineResolver.Resolve(await ResolveCommandExchangeAsync(cancellationToken));

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
        var executionEngine = _executionEngineResolver.Resolve(await ResolveCommandExchangeAsync(cancellationToken));

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
        var executionEngine = _executionEngineResolver.Resolve(await ResolveCommandExchangeAsync(cancellationToken));

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

        var executionEngine = _executionEngineResolver.Resolve(await ResolveCommandExchangeAsync(cancellationToken));

        try
        {
            // Trigger orders use the same cancel mechanism as regular orders
            await executionEngine.CancelOrderAsync(command.CancelPayload.OrderId, command.CancelPayload.Asset, cancellationToken);
            _logger.LogInformation("Trigger order cancelled: OrderId={OrderId}, Asset={Asset}", command.CancelPayload.OrderId, command.CancelPayload.Asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cancel trigger order failed: OrderId={OrderId}", command.CancelPayload.OrderId);
        }
    }

    internal async Task ApplyNetworkConfigAsync(NetworkConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        TradingSession? replacementSession = null;

        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            lock (_networkConfigLock)
            {
                if (_hyperliquidOptions.BaseUrl == config.BaseUrl &&
                    _hyperliquidOptions.WsBaseUrl == config.WsBaseUrl &&
                    _hyperliquidOptions.Network == config.Network)
                {
                    return;
                }
            }

            var strategyToRestart = _activeSession is not null &&
                ResolveStrategyExchange(_activeSession.StrategyConfig) == Exchange.Hyperliquid
                ? _activeSession.StrategyConfig
                : null;

            if (strategyToRestart is not null)
            {
                _logger.LogWarning(
                    "Network config changed to {Network}; restarting active Hyperliquid session to rebuild clients.",
                    config.Network);

                await _activeSession!.StopAsync();
                await _activeSession.DisposeAsync();
                _activeSession = null;
            }

            lock (_networkConfigLock)
            {
                _hyperliquidOptions.BaseUrl = config.BaseUrl;
                _hyperliquidOptions.WsBaseUrl = config.WsBaseUrl;
                _hyperliquidOptions.Network = config.Network;
            }

            _logger.LogInformation(
                "Network config updated from control plane: {Network} (REST={BaseUrl}, WS={WsBaseUrl})",
                config.Network, config.BaseUrl, config.WsBaseUrl);

            if (strategyToRestart is not null)
            {
                replacementSession = CreateSession(strategyToRestart);
                _activeSession = replacementSession;
            }
        }
        finally
        {
            _sessionLock.Release();
        }

        replacementSession?.Start();
    }

    private TradingSession CreateSession(StrategyConfig strategyConfig)
    {
        var exchange = ResolveStrategyExchange(strategyConfig);
        var gridState = new GridState();
        var orderTracker = _serviceProvider.GetRequiredService<IOrderTracker>();
        var riskEngine = _serviceProvider.GetRequiredService<IRiskEngine>();
        riskEngine.Reset();
        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
        var executionEngine = _executionEngineResolver.Resolve(exchange, strategyConfig.AssetType);
        var marketMetadataProvider = _serviceProvider.GetRequiredKeyedService<IExchangeMarketMetadataProvider>(exchange.ToString());
        var historicalDataClient = _serviceProvider.GetRequiredKeyedService<IExchangeHistoricalDataClient>(exchange.ToString());
        var accountClient = _serviceProvider.GetRequiredKeyedService<IExchangeAccountClient>(exchange.ToString());
        var symbolMapper = _serviceProvider.GetServices<IExchangeSymbolMapper>()
            .First(mapper => mapper.Exchange == exchange);

        // Create a scope for scoped repository services — owned by TradingSession
        var scope = _serviceProvider.CreateScope();
        var userId = _signerProvider.IsConfigured ? _signerProvider.WalletAddress : null;

        var triggerOrderManager = new TriggerOrderManager(
            executionEngine,
            loggerFactory.CreateLogger<TriggerOrderManager>());

        var positionManager = new LivePositionManager(
            executionEngine,
            orderTracker,
            riskEngine,
            loggerFactory.CreateLogger<LivePositionManager>(),
            triggerOrderManager);

        var stateRecoveryService = new StateRecoveryService(
            scope.ServiceProvider.GetRequiredService<IGridCycleRepository>(),
            scope.ServiceProvider.GetRequiredService<ILiveOrderRepository>(),
            accountClient,
            loggerFactory.CreateLogger<StateRecoveryService>());

        var contextBuilder = new LiveMarketContextBuilder(
            _serviceProvider.GetService<ILlmContextProvider>(),
            _serviceProvider.GetService<IFearGreedSnapshotProvider>(),
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            marketMetadataProvider,
            loggerFactory.CreateLogger<LiveMarketContextBuilder>());

        var fillProcessor = new FillProcessor(
            orderTracker,
            gridState,
            loggerFactory.CreateLogger<FillProcessor>(),
            riskEngine,
            scope.ServiceProvider.GetService<ILiveOrderRepository>(),
            scope.ServiceProvider.GetService<ILiveFillRepository>(),
            scope.ServiceProvider.GetService<IGridCycleRepository>(),
            userId,
            executionEngine);

        positionManager.ConfigureRepositories(
            scope.ServiceProvider.GetService<IGridCycleRepository>(),
            scope.ServiceProvider.GetService<ILiveOrderRepository>(),
            userId);
        positionManager.ConfigureProtectionState(gridState.ProtectionOrders);

        return new TradingSession(
            strategyConfig,
            exchange,
            exchange == Exchange.Hyperliquid ? _serviceProvider.GetRequiredService<IHyperliquidWebSocketClient>() : null,
            exchange == Exchange.Hyperliquid ? _serviceProvider.GetRequiredService<IHyperliquidUserEventClient>() : null,
            exchange == Exchange.Hyperliquid ? _serviceProvider.GetRequiredService<IHyperliquidRestClient>() : null,
            historicalDataClient,
            accountClient,
            symbolMapper,
            _serviceProvider.GetRequiredService<CandleBuilder>(),
            _serviceProvider.GetRequiredService<CandleClock>(),
            contextBuilder,
            _serviceProvider.GetRequiredService<IStrategyEngine>(),
            _serviceProvider.GetRequiredService<IGridController>(),
            riskEngine,
            positionManager,
            _serviceProvider.GetRequiredService<ISignalController>(),
            executionEngine,
            fillProcessor,
            _signerProvider,
            _healthProvider,
            loggerFactory.CreateLogger<TradingSession>(),
            gridState,
            stateRecoveryService,
            orderTracker,
            scope,
            triggerOrderManager,
            _serviceProvider.GetRequiredService<IOptions<RiskLimitsConfig>>(),
            _executionLogger,
            _serviceProvider.GetRequiredService<IDcaController>());
    }

    private async Task<Exchange> ResolveCommandExchangeAsync(CancellationToken cancellationToken = default)
    {
        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            return _activeSession is null
                ? Exchange.Hyperliquid
                : ResolveStrategyExchange(_activeSession.StrategyConfig);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private static Exchange ResolveStrategyExchange(StrategyConfig strategyConfig)
    {
        return Enum.TryParse<Exchange>(strategyConfig.Exchange, ignoreCase: true, out var exchange)
            ? exchange
            : Exchange.Hyperliquid;
    }

    private static string GetAgentVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;
        return version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
    }

    public override void Dispose()
    {
        _sessionLock.Dispose();
        base.Dispose();
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

    /// <summary>Shared secret used to authenticate control-plane requests.</summary>
    public string? SecretKey { get; set; }
}
