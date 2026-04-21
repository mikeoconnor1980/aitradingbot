using System.Net.Http.Headers;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using TradePilot.AI.Services;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MacroCalendar.Services;
using TradePilot.Application.Scheduling;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.StrategyAuthoring.Validation;
using TradePilot.Application.Trading.Signals.Abstractions;
using TradePilot.Application.Trading.Signals.Implementations;
using TradePilot.Application.Trading.Signals.Registry;
using TradePilot.Application.Trading.Services;
using TradePilot.Infrastructure.Binance;
using TradePilot.Infrastructure.Hyperliquid;
using TradePilot.Infrastructure.Security;
using TradePilot.Infrastructure.Services;
using TradePilot.Persistence;
using TradePilot.Persistence.Services;
using TradePilot.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

// When running as a Windows Service, the working directory is System32.
// Set the content root to the exe directory so appsettings.json is found.
if (WindowsServiceHelpers.IsWindowsService())
{
    var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
    var exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
    builder.Configuration.SetBasePath(exeDir);
    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    builder.Configuration.AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();
}

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "TradePilot Execution Agent";
});

builder.Services.AddDataProtection();
builder.Services.AddScoped<ICredentialEncryptionService, DataProtectionCredentialEncryptionService>();

// ---------- Persistence ----------
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddScoped<IMacroCalendarQueryService, MacroCalendarQueryService>();

// ---------- Hyperliquid configuration ----------
builder.Services.AddOptions<HyperliquidOptions>()
    .Bind(builder.Configuration.GetSection(HyperliquidOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BinanceTradingOptions>()
    .Bind(builder.Configuration.GetSection(BinanceTradingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ---------- Agent configuration ----------
builder.Services.AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName));

// ---------- Wallet signer (configurable at runtime) ----------
builder.Services.AddSingleton<MutableSignerProvider>();
builder.Services.AddSingleton<ISignerProvider>(sp => sp.GetRequiredService<MutableSignerProvider>());
builder.Services.AddSingleton<IHyperliquidSigner>(sp => sp.GetRequiredService<MutableSignerProvider>());

var privateKey = builder.Configuration
    .GetSection(HyperliquidOptions.SectionName)["PrivateKey"];

if (string.IsNullOrWhiteSpace(privateKey))
{
    Console.WriteLine(
        "WARNING: Hyperliquid:PrivateKey is not configured. " +
        "Set 'Hyperliquid__PrivateKey' environment variable or run install.ps1.");
}

builder.Services.AddHttpClient<IHyperliquidRestClient, HyperliquidRestClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<HyperliquidOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddResilienceHandler("hyperliquid-retry", pipelineBuilder =>
{
    pipelineBuilder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 5,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(60),
        UseJitter = true,
        ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests
            || (args.Outcome.Result is not null && (int)args.Outcome.Result.StatusCode >= 500)),
    });

        // Per-attempt timeout: each HTTP request attempt is capped at 5 seconds.
        // Retries can extend total operation time, which is intentional for transient failures.
    pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(5));
});

builder.Services.AddSingleton<INonceProvider, NonceProvider>();
builder.Services.AddSingleton<IHyperliquidWebSocketClient, HyperliquidWebSocketClient>();
builder.Services.AddSingleton<IHyperliquidUserEventClient, HyperliquidUserEventClient>();
builder.Services.AddSingleton<IFearGreedSnapshotProvider, ControlPlaneFearGreedSnapshotProvider>();
builder.Services.AddSingleton<IHyperliquidAccountService, HyperliquidAccountService>();
builder.Services.AddSingleton<HyperliquidAccountAdapter>();
builder.Services.AddSingleton<HyperliquidMarketMetadataProvider>();
builder.Services.AddSingleton<HyperliquidHistoricalDataClient>();
builder.Services.AddSingleton<IExchangeAccountClient>(sp => sp.GetRequiredService<HyperliquidAccountAdapter>());
builder.Services.AddSingleton<IExchangeMarketMetadataProvider>(sp => sp.GetRequiredService<HyperliquidMarketMetadataProvider>());
builder.Services.AddSingleton<IExchangeHistoricalDataClient>(sp => sp.GetRequiredService<HyperliquidHistoricalDataClient>());
builder.Services.AddKeyedSingleton<IExchangeAccountClient>("Hyperliquid", (sp, _) => sp.GetRequiredService<HyperliquidAccountAdapter>());
builder.Services.AddKeyedSingleton<IExchangeMarketMetadataProvider>("Hyperliquid", (sp, _) => sp.GetRequiredService<HyperliquidMarketMetadataProvider>());
builder.Services.AddKeyedSingleton<IExchangeHistoricalDataClient>("Hyperliquid", (sp, _) => sp.GetRequiredService<HyperliquidHistoricalDataClient>());

builder.Services.AddSingleton<IExchangeSymbolMapper, HyperliquidExchangeSymbolMapper>();
builder.Services.AddSingleton<IExchangeSymbolMapper, BinanceAssetMapper>();
builder.Services.AddSingleton<IExchangeCredentialAccessor, AgentExchangeCredentialAccessor>();
builder.Services.AddTransient<WorkerBinanceSigningHandler>();
builder.Services.AddSingleton<IBinanceExchangeInfoCache, BinanceExchangeInfoCache>();

builder.Services.AddHttpClient("binance-public", (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<BinanceTradingOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IBinanceFuturesRestClient, BinanceFuturesRestClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<BinanceTradingOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddResilienceHandler("binance-public-retry", pipelineBuilder =>
{
    pipelineBuilder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 5,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(60),
        UseJitter = true,
        ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
            (args.Outcome.Result is not null && (int)args.Outcome.Result.StatusCode >= 500)),
    });

    // Per-attempt timeout: each HTTP request attempt is capped at 5 seconds.
    // Retries can extend total operation time, which is intentional for transient failures.
    pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(5));
});

builder.Services.AddHttpClient<IBinanceFuturesAuthClient, BinanceFuturesAuthClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<BinanceTradingOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<WorkerBinanceSigningHandler>()
.AddResilienceHandler("binance-auth-retry", pipelineBuilder =>
{
    pipelineBuilder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 5,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(60),
        UseJitter = true,
        ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
            args.Outcome.Result?.StatusCode == (System.Net.HttpStatusCode)418 ||
            (args.Outcome.Result is not null && (int)args.Outcome.Result.StatusCode >= 500)),
    });

    // Per-attempt timeout: each HTTP request attempt is capped at 5 seconds.
    // Retries can extend total operation time, which is intentional for transient failures.
    pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(5));
});

builder.Services.AddSingleton<BinanceAccountAdapter>();
builder.Services.AddSingleton<BinanceMarketMetadataProvider>();
builder.Services.AddSingleton<BinanceHistoricalDataClient>();
builder.Services.AddKeyedSingleton<IExchangeAccountClient>("Binance", (sp, _) => sp.GetRequiredService<BinanceAccountAdapter>());
builder.Services.AddKeyedSingleton<IExchangeMarketMetadataProvider>("Binance", (sp, _) => sp.GetRequiredService<BinanceMarketMetadataProvider>());
builder.Services.AddKeyedSingleton<IExchangeHistoricalDataClient>("Binance", (sp, _) => sp.GetRequiredService<BinanceHistoricalDataClient>());

// ---------- Execution engine (signs + submits orders locally) ----------
builder.Services.AddSingleton<LiveExecutionEngine>();
builder.Services.AddSingleton<BinanceExecutionEngine>();
builder.Services.AddSingleton<IExecutionEngine>(sp => sp.GetRequiredService<LiveExecutionEngine>());
builder.Services.AddKeyedSingleton<IExecutionEngine>("Hyperliquid", (sp, _) => sp.GetRequiredService<LiveExecutionEngine>());
builder.Services.AddKeyedSingleton<IExecutionEngine>("Binance", (sp, _) => sp.GetRequiredService<BinanceExecutionEngine>());
builder.Services.AddSingleton<IExecutionEngineResolver, ExchangeExecutionEngineResolver>();

// ---------- LLM context provider (optional — runs with synthetic fallback if unconfigured) ----------
var llmContextSection = builder.Configuration.GetSection(LlmContextOptions.SectionName);
if (llmContextSection.Exists() && !string.IsNullOrWhiteSpace(llmContextSection["ApiKey"]))
{
    builder.Services.AddOptions<LlmContextOptions>()
        .Bind(llmContextSection)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddHttpClient<ILlmContextClient, LlmContextClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<LlmContextOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }
    });

    builder.Services.AddSingleton<ILlmContextProvider, LlmContextProvider>();
}

// ---------- Candle pipeline ----------
builder.Services.AddSingleton<MarketStateStore>();
builder.Services.AddSingleton<CandleClock>();
builder.Services.AddSingleton<CandleBuilder>();

// ---------- Strategy / trading pipeline ----------
builder.Services.AddSingleton<IMarketContextBuilder>(sp =>
    new LiveMarketContextBuilder(
        sp.GetService<ILlmContextProvider>(),
        sp.GetService<IFearGreedSnapshotProvider>(),
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<IExchangeMarketMetadataProvider>(),
        sp.GetRequiredService<ILoggerFactory>().CreateLogger<LiveMarketContextBuilder>()));
builder.Services.AddSingleton<IOrderTracker, InMemoryOrderTracker>();
builder.Services.AddScoped<IStateRecoveryService, StateRecoveryService>();
builder.Services.AddSingleton<IPositionManager, LivePositionManager>();
builder.Services.AddSingleton<LiveExecutionLogger>();
builder.Services.AddSingleton<IExecutionLogger>(sp => sp.GetRequiredService<LiveExecutionLogger>());
builder.Services.AddSingleton<GridStrategyEngine>();
builder.Services.AddSingleton<DcaStrategyEngine>();
builder.Services.AddSingleton<IDerivedSignal, CandlePatternSignal>();
builder.Services.AddSingleton<IDerivedSignal, LiquiditySweepSignal>();
builder.Services.AddSingleton<IDerivedSignal, StructureShiftSignal>();
builder.Services.AddSingleton<IDerivedSignalRegistry>(sp =>
{
    var registry = new DerivedSignalRegistry();
    foreach (var signal in sp.GetServices<IDerivedSignal>())
    {
        registry.Register(signal);
    }

    return registry;
});
builder.Services.AddSingleton<IConditionHandler, RsiConditionHandler>();
builder.Services.AddSingleton<IConditionHandler, PriceVsEmaConditionHandler>();
builder.Services.AddSingleton<IConditionHandler, MacdConditionHandler>();
builder.Services.AddSingleton<IConditionHandler, SupportResistanceConditionHandler>();
builder.Services.AddSingleton<IConditionHandler, DerivedSignalConditionHandler>();
builder.Services.AddSingleton<IConditionEvaluator, ConditionEvaluator>();
builder.Services.AddSingleton<ITrendFilterEvaluator, TrendFilterEvaluator>();
builder.Services.AddSingleton<IStrategyEngine, CompositeStrategyEngine>();
builder.Services.AddSingleton<IGridController, GridController>();
builder.Services.AddSingleton<ISignalController, SignalController>();
builder.Services.AddSingleton<IDcaController, DcaController>();
builder.Services.AddSingleton<ITriggerOrderManager, TriggerOrderManager>();

// ---------- Risk engine (live limits: daily loss, order size, circuit breaker) ----------
builder.Services.AddOptions<RiskLimitsConfig>()
    .Bind(builder.Configuration.GetSection(RiskLimitsConfig.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.PostConfigure<RiskLimitsConfig>(options =>
{
    RiskLimitsConfigDefaults.Apply(options);
});
builder.Services.AddSingleton<IValidateOptions<RiskLimitsConfig>, RiskLimitsConfigValidator>();
builder.Services.AddSingleton<LiveRiskEngine>();
builder.Services.AddSingleton<IRiskEngine>(sp => sp.GetRequiredService<LiveRiskEngine>());

// ---------- Health monitoring ----------
builder.Services.AddSingleton<TradingHealthProvider>();
builder.Services.AddSingleton<ITradingHealthProvider>(sp => sp.GetRequiredService<TradingHealthProvider>());
builder.Services.AddHostedService<HealthMonitorService>();

// ---------- Agent check-in (polls API for commands, manages trading sessions) ----------
builder.Services.AddHttpClient(AgentCheckInService.HttpClientName, (sp, client) =>
{
    var agentOptions = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    AgentCheckInService.ConfigureControlPlaneHttpClient(client, agentOptions);
});

// ---------- Auto-update service ----------
builder.Services.AddHttpClient(UpdateCheckerService.UpdateDownloadHttpClientName, client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddSingleton<UpdateCheckerService>();
builder.Services.AddSingleton<IUpdateNotifier>(sp => sp.GetRequiredService<UpdateCheckerService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<UpdateCheckerService>());

// ---------- Telegram notifications ----------
builder.Services.AddSingleton<NotificationConfigHolder>();

// Telegram notifier — uses bot token received dynamically from the API heartbeat.
// No local bot token config needed; the token flows from control plane via NotificationConfigHolder.
builder.Services.AddHttpClient("TelegramBot", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSingleton<ITelegramNotifier, DynamicTelegramNotifier>();
builder.Services.AddSingleton<INotificationDispatcher, NotificationDispatcher>();

builder.Services.AddHostedService<AgentCheckInService>();

// ---------- Azure SignalR publisher (pushes real-time data to browser via Azure SignalR Service) ----------
var signalRConnectionString = builder.Configuration["Azure:SignalR:ConnectionString"];
if (!string.IsNullOrWhiteSpace(signalRConnectionString))
{
    var serviceManager = new ServiceManagerBuilder()
        .WithOptions(option =>
        {
            option.ConnectionString = signalRConnectionString;
            option.ServiceTransportType = ServiceTransportType.Persistent;
        })
        .BuildServiceManager();

    builder.Services.AddSingleton(serviceManager);
    builder.Services.AddSingleton<ISignalRPublisher, AzureSignalRPublisher>();
    builder.Services.AddHostedService<MarketDataStreamService>();
}
else
{
    builder.Services.AddSingleton<ISignalRPublisher, RelaySignalRPublisher>();
}

// User event WebSocket — runs regardless of SignalR (needed for Telegram notifications)
builder.Services.AddHostedService<UserEventStreamService>();

var app = builder.Build();

// Configure wallet signer if key is available from config/env
if (!string.IsNullOrWhiteSpace(privateKey))
{
    app.Services.GetRequiredService<MutableSignerProvider>().Configure(privateKey);
}

await app.Services.MigrateDatabaseAsync();

app.Run();
