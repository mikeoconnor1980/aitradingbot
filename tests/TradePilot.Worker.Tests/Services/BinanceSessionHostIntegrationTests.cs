using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Scheduling;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Validation;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Infrastructure.Binance;
using TradePilot.Infrastructure.Services;
using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

[TestClass]
public sealed class BinanceSessionHostIntegrationTests
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private const string WalletAddress = "0xb63a3948477254cc17E0fb444050B9E161FCcFA3";

    [TestMethod]
    public async Task GivenBinanceStrategySession_WhenHostBoots_ThenSessionResolvesBinanceExecutionEngineEndToEnd()
    {
        var authHandler = new CapturingHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/fapi/v1/order")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"orderId":12345,"status":"NEW"}""", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            };
        });

                var publicHandler = new CapturingHttpMessageHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
                {
                        Content = new StringContent(
                                request.RequestUri?.AbsolutePath == "/fapi/v1/exchangeInfo"
                                        ? """
                                            {
                                                "symbols": [
                                                    {
                                                        "symbol": "BTCUSDT",
                                                        "baseAsset": "BTC",
                                                        "quoteAsset": "USDT",
                                                        "status": "TRADING",
                                                        "filters": [
                                                            {
                                                                "filterType": "LOT_SIZE",
                                                                "stepSize": "0.001"
                                                            },
                                                            {
                                                                "filterType": "PRICE_FILTER",
                                                                "tickSize": "0.10"
                                                            }
                                                        ]
                                                    }
                                                ]
                                            }
                                            """
                                        : "{}",
                                Encoding.UTF8,
                                "application/json")
                });

        var walletRepository = new Mock<IUserWalletAddressRepository>();
        var credentialRepository = new Mock<IUserExchangeCredentialRepository>();
        var encryptionService = new Mock<ICredentialEncryptionService>();
        var signerProvider = new Mock<ISignerProvider>();
        var tradingHealthProvider = new Mock<ITradingHealthProvider>();
        var updateNotifier = new Mock<IUpdateNotifier>();
        var telegramNotifier = new Mock<ITelegramNotifier>();
        var notificationDispatcher = new Mock<INotificationDispatcher>();
        var riskEngine = new Mock<IRiskEngine>();
        var strategyEngine = new Mock<IStrategyEngine>();
        var gridController = new Mock<IGridController>();
        var signalController = new Mock<ISignalController>();
        var dcaController = new Mock<IDcaController>();
        var gridCycleRepository = new Mock<IGridCycleRepository>();
        var liveOrderRepository = new Mock<ILiveOrderRepository>();
        var liveFillRepository = new Mock<ILiveFillRepository>();

        signerProvider.SetupGet(provider => provider.IsConfigured).Returns(true);
        signerProvider.SetupGet(provider => provider.WalletAddress).Returns(WalletAddress);

        walletRepository
            .Setup(repository => repository.GetActiveByWalletAddressAsync(WalletAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserWalletAddress.Create(TestUserId, WalletAddress));

        credentialRepository
            .Setup(repository => repository.GetActiveByUserIdAndExchangeAsync(TestUserId, Exchange.Binance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserExchangeCredential.Create(TestUserId, Exchange.Binance, "binance-api-key", "encrypted-secret", "Primary Binance"));

        encryptionService
            .Setup(service => service.Decrypt("encrypted-secret"))
            .Returns("super-secret");

        using var host = BuildHost(
            authHandler,
            publicHandler,
            walletRepository.Object,
            credentialRepository.Object,
            encryptionService.Object,
            signerProvider.Object,
            tradingHealthProvider.Object,
            updateNotifier.Object,
            telegramNotifier.Object,
            notificationDispatcher.Object,
            riskEngine.Object,
            strategyEngine.Object,
            gridController.Object,
            signalController.Object,
            dcaController.Object,
            gridCycleRepository.Object,
            liveOrderRepository.Object,
            liveFillRepository.Object);

        await host.StartAsync();

        var agentService = host.Services.GetRequiredService<AgentCheckInService>();
        var createSession = typeof(AgentCheckInService).GetMethod("CreateSession", BindingFlags.Instance | BindingFlags.NonPublic);
        createSession.Should().NotBeNull();

        var strategyConfig = new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            StrategyName = "binance-session",
            Exchange = Exchange.Binance.ToString(),
            Market = "BTC-PERP",
            Timeframe = "15m",
            Direction = Direction.Long,
        };

        var session = (TradingSession?)createSession!.Invoke(agentService, [strategyConfig]);
        session.Should().NotBeNull();

        var sessionEngine = GetPrivateField<IExecutionEngine>(session!, "_executionEngine");
        sessionEngine.Should().BeOfType<BinanceExecutionEngine>();

        var positionManager = GetPrivateField<LivePositionManager>(session!, "_positionManager");
        var positionManagerEngine = GetPrivateField<IExecutionEngine>(positionManager, "_executionEngine");
        positionManagerEngine.Should().BeSameAs(sessionEngine);

        var fillProcessor = GetPrivateField<FillProcessor>(session!, "_fillProcessor");
        var fillProcessorEngine = GetPrivateField<IExecutionEngine?>(fillProcessor, "_executionEngine");
        fillProcessorEngine.Should().BeSameAs(sessionEngine);

        var orderId = await sessionEngine.PlaceOrderAsync(
            new OrderRequest
            {
                Symbol = "BTC-PERP",
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Price = 0m,
                Size = 0.01m,
                TradeType = TradeType.Manual,
            },
            CancellationToken.None);

        orderId.Should().Be("12345");
        authHandler.LastRequest.Should().NotBeNull();
        authHandler.LastRequest!.Headers.GetValues("X-MBX-APIKEY").Single().Should().Be("binance-api-key");
        authHandler.LastRequest.RequestUri!.Query.Should().Contain("signature=");
        authHandler.LastRequest.RequestUri!.Query.Should().Contain("timestamp=");
        authHandler.LastRequest.RequestUri!.Query.Should().Contain("recvWindow=5000");
        authHandler.LastRequest.RequestUri!.Query.Should().Contain("symbol=BTCUSDT");

        await session!.DisposeAsync();
        await host.StopAsync();
    }

    private static IHost BuildHost(
        HttpMessageHandler authHandler,
        HttpMessageHandler publicHandler,
        IUserWalletAddressRepository walletRepository,
        IUserExchangeCredentialRepository credentialRepository,
        ICredentialEncryptionService credentialEncryptionService,
        ISignerProvider signerProvider,
        ITradingHealthProvider tradingHealthProvider,
        IUpdateNotifier updateNotifier,
        ITelegramNotifier telegramNotifier,
        INotificationDispatcher notificationDispatcher,
        IRiskEngine riskEngine,
        IStrategyEngine strategyEngine,
        IGridController gridController,
        ISignalController signalController,
        IDcaController dcaController,
        IGridCycleRepository gridCycleRepository,
        ILiveOrderRepository liveOrderRepository,
        ILiveFillRepository liveFillRepository)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddLogging();
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<IOptions<HyperliquidOptions>>(Options.Create(new HyperliquidOptions()));
        builder.Services.AddSingleton<IOptions<BinanceTradingOptions>>(Options.Create(new BinanceTradingOptions()));
        builder.Services.AddSingleton<IOptions<AgentOptions>>(Options.Create(new AgentOptions()));
        builder.Services.AddSingleton<IOptions<RiskLimitsConfig>>(Options.Create(new RiskLimitsConfig()));
        builder.Services.AddSingleton<IValidateOptions<RiskLimitsConfig>, RiskLimitsConfigValidator>();
        builder.Services.PostConfigure<RiskLimitsConfig>(RiskLimitsConfigDefaults.Apply);

        builder.Services.AddSingleton(walletRepository);
        builder.Services.AddSingleton(credentialRepository);
        builder.Services.AddSingleton(credentialEncryptionService);
        builder.Services.AddSingleton(signerProvider);
        builder.Services.AddSingleton(tradingHealthProvider);
        builder.Services.AddSingleton(updateNotifier);
        builder.Services.AddSingleton<IExecutionLogger>(NullExecutionLogger.Instance);
        builder.Services.AddSingleton(telegramNotifier);
        builder.Services.AddSingleton(notificationDispatcher);
        builder.Services.AddSingleton(new NotificationConfigHolder());
        builder.Services.AddSingleton(riskEngine);
        builder.Services.AddSingleton(strategyEngine);
        builder.Services.AddSingleton(gridController);
        builder.Services.AddSingleton(signalController);
        builder.Services.AddSingleton(dcaController);
        builder.Services.AddScoped(_ => gridCycleRepository);
        builder.Services.AddScoped(_ => liveOrderRepository);
        builder.Services.AddScoped(_ => liveFillRepository);

        builder.Services.AddSingleton<IExchangeCredentialAccessor, AgentExchangeCredentialAccessor>();
        builder.Services.AddTransient<WorkerBinanceSigningHandler>();
        builder.Services.AddSingleton<IExchangeSymbolMapper, BinanceAssetMapper>();
        builder.Services.AddSingleton<IBinanceExchangeInfoCache, BinanceExchangeInfoCache>();

        builder.Services.AddHttpClient("binance-public", client =>
        {
            client.BaseAddress = new Uri("https://fapi.binance.com");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => publicHandler);

        builder.Services.AddHttpClient<IBinanceFuturesRestClient, BinanceFuturesRestClient>(client =>
        {
            client.BaseAddress = new Uri("https://fapi.binance.com");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => publicHandler);

        builder.Services.AddHttpClient<IBinanceFuturesAuthClient, BinanceFuturesAuthClient>(client =>
        {
            client.BaseAddress = new Uri("https://fapi.binance.com");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => authHandler)
        .AddHttpMessageHandler<WorkerBinanceSigningHandler>();

        builder.Services.AddSingleton<BinanceAccountAdapter>();
        builder.Services.AddSingleton<BinanceMarketMetadataProvider>();
        builder.Services.AddSingleton<BinanceHistoricalDataClient>();
        builder.Services.AddSingleton<BinanceExecutionEngine>();
        builder.Services.AddKeyedSingleton<IExchangeAccountClient>("Binance", (sp, _) => sp.GetRequiredService<BinanceAccountAdapter>());
        builder.Services.AddKeyedSingleton<IExchangeMarketMetadataProvider>("Binance", (sp, _) => sp.GetRequiredService<BinanceMarketMetadataProvider>());
        builder.Services.AddKeyedSingleton<IExchangeHistoricalDataClient>("Binance", (sp, _) => sp.GetRequiredService<BinanceHistoricalDataClient>());
        builder.Services.AddKeyedSingleton<IExecutionEngine>("Binance", (sp, _) => sp.GetRequiredService<BinanceExecutionEngine>());
        builder.Services.AddSingleton<IExecutionEngineResolver, ExchangeExecutionEngineResolver>();

        builder.Services.AddSingleton<MarketStateStore>();
        builder.Services.AddSingleton<CandleClock>();
        builder.Services.AddSingleton<CandleBuilder>();
        builder.Services.AddSingleton<IOrderTracker, InMemoryOrderTracker>();
        builder.Services.AddSingleton<AgentCheckInService>();

        return builder.Build();
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"Expected private field '{fieldName}' on {instance.GetType().Name}.");
        return (T)field!.GetValue(instance)!;
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