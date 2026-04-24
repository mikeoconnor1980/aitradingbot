using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Scheduling;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Enums;
using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

[TestClass]
public sealed class AgentCheckInServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
    private readonly Mock<IServiceProvider> _serviceProvider = new();
    private readonly Mock<IExecutionEngineResolver> _executionEngineResolver = new();
    private readonly Mock<IExecutionEngine> _executionEngine = new();
    private readonly Mock<ISignerProvider> _signerProvider = new();
    private readonly Mock<ITradingHealthProvider> _healthProvider = new();
    private readonly Mock<IUpdateNotifier> _updateNotifier = new();
    private readonly Mock<ITelegramNotifier> _telegramNotifier = new();
    private readonly Mock<INotificationDispatcher> _notificationDispatcher = new();

    [TestMethod]
    public async Task GivenSecretKeyConfigured_WhenHeartbeatSent_ThenAuthorizationHeaderIncluded()
    {
        var handler = new CapturingHttpMessageHandler(_ => CreateHeartbeatResponse());
        var client = CreateControlPlaneClient(handler, new AgentOptions
        {
            AgentId = "agent-test",
            ControlPlaneUrl = "http://localhost:5062",
            SecretKey = "shared-secret",
        });

        var sut = CreateSut(client, new AgentOptions
        {
            AgentId = "agent-test",
            ControlPlaneUrl = "http://localhost:5062",
            SecretKey = "shared-secret",
        });

        await InvokeCheckInAsync(sut);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Authorization.Should().NotBeNull();
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("shared-secret");
    }

    [TestMethod]
    public async Task GivenNoSecretKey_WhenHeartbeatSent_ThenNoAuthorizationHeader()
    {
        var handler = new CapturingHttpMessageHandler(_ => CreateHeartbeatResponse());
        var client = CreateControlPlaneClient(handler, new AgentOptions
        {
            AgentId = "agent-test",
            ControlPlaneUrl = "http://localhost:5062",
        });

        var sut = CreateSut(client, new AgentOptions
        {
            AgentId = "agent-test",
            ControlPlaneUrl = "http://localhost:5062",
        });

        await InvokeCheckInAsync(sut);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Authorization.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenCancelTriggerOrderCommand_WhenHandled_ThenUsesExchangeAwareExecutionEngineResolver()
    {
        // Arrange
        _executionEngineResolver
            .Setup(resolver => resolver.Resolve(Exchange.Hyperliquid))
            .Returns(_executionEngine.Object);
        _executionEngine
            .Setup(engine => engine.CancelOrderAsync("trigger-1", "BTC-PERP", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(new HttpClient(), CreateAgentOptions());
        var command = new AgentCommand
        {
            CommandId = "cmd-cancel-trigger",
            AgentId = "agent-test",
            Type = AgentCommandType.CancelTriggerOrder,
            CancelPayload = new CancelOrderPayload
            {
                OrderId = "trigger-1",
                Asset = "BTC-PERP"
            }
        };

        // Act
        await InvokeHandleCommandAsync(sut, command);

        // Assert
        _executionEngineResolver.Verify(resolver => resolver.Resolve(Exchange.Hyperliquid), Times.Once);
        _executionEngine.Verify(
            engine => engine.CancelOrderAsync("trigger-1", "BTC-PERP", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenWalletNotConfigured_WhenStartCommandHandled_ThenFailedOrderResultIsQueued()
    {
        // Arrange
        _signerProvider.SetupGet(provider => provider.IsConfigured).Returns(false);
        var sut = CreateSut(new HttpClient(), CreateAgentOptions());
        var command = new AgentCommand
        {
            CommandId = "cmd-start",
            AgentId = "agent-test",
            Type = AgentCommandType.Start,
            StrategyConfig = new StrategyConfig
            {
                StrategyName = "Test Strategy",
                Market = "BTC-PERP",
                Timeframe = "15m"
            }
        };

        // Act
        await InvokeHandleCommandAsync(sut, command);
        var heartbeat = await InvokeBuildHeartbeatAsync(sut);

        // Assert
        heartbeat.OrderResults.Should().ContainSingle(result =>
            result.CommandId == "cmd-start" &&
            result.Success == false &&
            result.Detail == "Wallet not configured on agent.");
    }

    [TestMethod]
    public async Task GivenSessionLockHeld_WhenBuildHeartbeatInvoked_ThenWaitsForRelease()
    {
        var sut = CreateSut(new HttpClient(), CreateAgentOptions());
        var sessionLock = GetSessionLock(sut);
        Task<AgentHeartbeat>? heartbeatTask = null;

        await sessionLock.WaitAsync();
        try
        {
            heartbeatTask = InvokeBuildHeartbeatTask(sut);
            await Task.Delay(50);

            heartbeatTask.IsCompleted.Should().BeFalse();
        }
        finally
        {
            sessionLock.Release();
        }

        var heartbeat = await heartbeatTask!;
        heartbeat.State.Should().Be(AgentState.Idle);
    }

    [TestMethod]
    public async Task GivenCreateSession_WhenInvoked_ThenRiskEngineResetCalled()
    {
        var riskEngine = new Mock<IRiskEngine>();
        var executionEngine = new Mock<IExecutionEngine>();
        var executionEngineResolver = new Mock<IExecutionEngineResolver>();
        executionEngineResolver
            .Setup(resolver => resolver.Resolve(Exchange.Binance))
            .Returns(executionEngine.Object);

        var serviceProvider = BuildSessionServiceProvider(riskEngine.Object);
        var signerProvider = new Mock<ISignerProvider>();
        signerProvider.SetupGet(provider => provider.IsConfigured).Returns(false);

        var sut = new AgentCheckInService(
            _httpClientFactory.Object,
            serviceProvider,
            executionEngineResolver.Object,
            signerProvider.Object,
            _healthProvider.Object,
            _updateNotifier.Object,
            NullExecutionLogger.Instance,
            _telegramNotifier.Object,
            _notificationDispatcher.Object,
            new NotificationConfigHolder(),
            Options.Create(new HyperliquidOptions()),
            Options.Create(CreateAgentOptions()),
            NullLogger<AgentCheckInService>.Instance);

        var session = InvokeCreateSession(sut, new StrategyConfig
        {
            StrategyName = "Reset Test",
            Market = "BTCUSDT",
            Timeframe = "15m",
            Exchange = Exchange.Binance.ToString(),
        });

        try
        {
            riskEngine.Verify(engine => engine.Reset(), Times.Once);
        }
        finally
        {
            await session.DisposeAsync();
            await serviceProvider.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task GivenChangedNetworkConfig_WhenAppliedWithoutActiveSession_ThenOptionsUpdated()
    {
        var hyperliquidOptions = new HyperliquidOptions
        {
            BaseUrl = "https://api.hyperliquid.xyz",
            WsBaseUrl = "wss://api.hyperliquid.xyz/ws",
            Network = "mainnet",
        };

        var sut = new AgentCheckInService(
            _httpClientFactory.Object,
            _serviceProvider.Object,
            _executionEngineResolver.Object,
            _signerProvider.Object,
            _healthProvider.Object,
            _updateNotifier.Object,
            NullExecutionLogger.Instance,
            _telegramNotifier.Object,
            _notificationDispatcher.Object,
            new NotificationConfigHolder(),
            Options.Create(hyperliquidOptions),
            Options.Create(CreateAgentOptions()),
            NullLogger<AgentCheckInService>.Instance);

        await sut.ApplyNetworkConfigAsync(new NetworkConfig
        {
            BaseUrl = "https://api.hyperliquid-testnet.xyz",
            WsBaseUrl = "wss://api.hyperliquid-testnet.xyz/ws",
            Network = "testnet",
        });

        hyperliquidOptions.BaseUrl.Should().Be("https://api.hyperliquid-testnet.xyz");
        hyperliquidOptions.WsBaseUrl.Should().Be("wss://api.hyperliquid-testnet.xyz/ws");
        hyperliquidOptions.Network.Should().Be("testnet");
    }

    private AgentCheckInService CreateSut(HttpClient client, AgentOptions agentOptions)
    {
        _httpClientFactory
            .Setup(factory => factory.CreateClient(AgentCheckInService.HttpClientName))
            .Returns(client);

        _signerProvider.SetupGet(provider => provider.IsConfigured).Returns(false);

        return new AgentCheckInService(
            _httpClientFactory.Object,
            _serviceProvider.Object,
            _executionEngineResolver.Object,
            _signerProvider.Object,
            _healthProvider.Object,
            _updateNotifier.Object,
            NullExecutionLogger.Instance,
            _telegramNotifier.Object,
            _notificationDispatcher.Object,
            new NotificationConfigHolder(),
            Options.Create(new HyperliquidOptions()),
            Options.Create(agentOptions),
            NullLogger<AgentCheckInService>.Instance);
    }

    private static AgentOptions CreateAgentOptions()
    {
        return new AgentOptions
        {
            AgentId = "agent-test",
            ControlPlaneUrl = "http://localhost:5062",
        };
    }

    private static HttpClient CreateControlPlaneClient(HttpMessageHandler handler, AgentOptions agentOptions)
    {
        var client = new HttpClient(handler);
        AgentCheckInService.ConfigureControlPlaneHttpClient(client, agentOptions);
        return client;
    }

    private static async Task InvokeCheckInAsync(AgentCheckInService sut)
    {
        var method = typeof(AgentCheckInService).GetMethod("CheckInAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var task = (Task?)method!.Invoke(sut, [CancellationToken.None]);
        task.Should().NotBeNull();
        await task!;
    }

    private static async Task InvokeHandleCommandAsync(AgentCheckInService sut, AgentCommand command)
    {
        var method = typeof(AgentCheckInService).GetMethod("HandleCommandAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var task = (Task?)method!.Invoke(sut, [command, CancellationToken.None]);
        task.Should().NotBeNull();
        await task!;
    }

    private static async Task<AgentHeartbeat> InvokeBuildHeartbeatAsync(AgentCheckInService sut)
    {
        var task = InvokeBuildHeartbeatTask(sut);
        return await task;
    }

    private static Task<AgentHeartbeat> InvokeBuildHeartbeatTask(AgentCheckInService sut)
    {
        var method = typeof(AgentCheckInService).GetMethod("BuildHeartbeatAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var task = method!.Invoke(sut, [CancellationToken.None]) as Task<AgentHeartbeat>;
        task.Should().NotBeNull();
        return task!;
    }

    private static SemaphoreSlim GetSessionLock(AgentCheckInService sut)
    {
        var field = typeof(AgentCheckInService).GetField("_sessionLock", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();

        var sessionLock = field!.GetValue(sut) as SemaphoreSlim;
        sessionLock.Should().NotBeNull();
        return sessionLock!;
    }

    private static TradingSession InvokeCreateSession(AgentCheckInService sut, StrategyConfig strategyConfig)
    {
        var method = typeof(AgentCheckInService).GetMethod("CreateSession", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var session = method!.Invoke(sut, [strategyConfig]) as TradingSession;
        session.Should().NotBeNull();
        return session!;
    }

    private static ServiceProvider BuildSessionServiceProvider(IRiskEngine riskEngine)
    {
        var services = new ServiceCollection();

        services.AddSingleton(riskEngine);
        services.AddSingleton<IOrderTracker, InMemoryOrderTracker>();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<MarketStateStore>();
        services.AddSingleton<CandleClock>();
        services.AddSingleton<CandleBuilder>(serviceProvider => new CandleBuilder(
            serviceProvider.GetRequiredService<MarketStateStore>(),
            serviceProvider.GetRequiredService<CandleClock>(),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CandleBuilder>.Instance));
        services.AddSingleton(Options.Create(new RiskLimitsConfig()));
        services.AddSingleton(Mock.Of<IStrategyEngine>());
        services.AddSingleton(Mock.Of<IGridController>());
        services.AddSingleton(Mock.Of<ISignalController>());
        services.AddSingleton(Mock.Of<IDcaController>());
        services.AddSingleton(Mock.Of<IGridCycleRepository>());
        services.AddSingleton(Mock.Of<ILiveOrderRepository>());
        services.AddSingleton(Mock.Of<ILiveFillRepository>());
        services.AddSingleton(Mock.Of<IExchangeSymbolMapper>(mapper => mapper.Exchange == Exchange.Binance));
        services.AddKeyedSingleton<IExchangeMarketMetadataProvider>(Exchange.Binance.ToString(), Mock.Of<IExchangeMarketMetadataProvider>());
        services.AddKeyedSingleton<IExchangeHistoricalDataClient>(Exchange.Binance.ToString(), Mock.Of<IExchangeHistoricalDataClient>());
        services.AddKeyedSingleton<IExchangeAccountClient>(Exchange.Binance.ToString(), Mock.Of<IExchangeAccountClient>());

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    private static HttpResponseMessage CreateHeartbeatResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responseFactory(request));
        }
    }
}