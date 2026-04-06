using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Agent.Models;
using TradingApp.Application.Scheduling;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Services;

namespace TradingApp.Worker.Services;

/// <summary>
/// Background service that periodically checks in with the API control plane.
/// Sends heartbeats and picks up pending commands (Start/Stop trading).
/// </summary>
public sealed class AgentCheckInService : BackgroundService
{
    private static readonly TimeSpan CheckInInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(15);

    internal const string HttpClientName = "ControlPlane";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISignerProvider _signerProvider;
    private readonly ITradingHealthProvider _healthProvider;
    private readonly AgentOptions _agentOptions;
    private readonly ILogger<AgentCheckInService> _logger;

    private TradingSession? _activeSession;
    private readonly object _sessionLock = new();

    public AgentCheckInService(
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ISignerProvider signerProvider,
        ITradingHealthProvider healthProvider,
        IOptions<AgentOptions> agentOptions,
        ILogger<AgentCheckInService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _signerProvider = signerProvider;
        _healthProvider = healthProvider;
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
            "/api/agent/heartbeat", heartbeat, cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(
            cancellationToken: cancellationToken);

        if (result?.PendingCommand is not null)
        {
            await HandleCommandAsync(result.PendingCommand, cancellationToken);
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

        return new AgentHeartbeat
        {
            AgentId = _agentOptions.AgentId,
            State = state,
            MachineName = Environment.MachineName,
            WalletAddress = _signerProvider.IsConfigured ? _signerProvider.WalletAddress : null,
            ActiveStrategy = activeStrategy,
            TimestampUtc = DateTimeOffset.UtcNow,
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

    private TradingSession CreateSession(StrategyConfig strategyConfig)
    {
        return new TradingSession(
            strategyConfig,
            _serviceProvider.GetRequiredService<IHyperliquidWebSocketClient>(),
            _serviceProvider.GetRequiredService<CandleBuilder>(),
            _serviceProvider.GetRequiredService<CandleClock>(),
            _serviceProvider.GetRequiredService<IMarketContextBuilder>(),
            _serviceProvider.GetRequiredService<IStrategyEngine>(),
            _serviceProvider.GetRequiredService<IGridController>(),
            _serviceProvider.GetRequiredService<IRiskEngine>(),
            _serviceProvider.GetRequiredService<IPositionManager>(),
            _serviceProvider.GetRequiredService<ISignalController>(),
            _serviceProvider.GetRequiredService<IExecutionEngine>(),
            _healthProvider,
            _logger);
    }
}

/// <summary>
/// Configuration for the agent check-in service.
/// </summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Unique identifier for this agent instance.</summary>
    public string AgentId { get; set; } = $"{Environment.MachineName}-{Guid.NewGuid():N}".ToLowerInvariant()[..20];

    /// <summary>Base URL of the API control plane.</summary>
    public string ControlPlaneUrl { get; set; } = "http://localhost:5062";
}
