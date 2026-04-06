using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Scheduling;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Services;
using TradingApp.Application.Trading.Services;
using TradingApp.Infrastructure.Services;
using TradingApp.Persistence;
using TradingApp.Worker.Services;

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
    options.ServiceName = "TradingApp Execution Agent";
});

// ---------- Persistence ----------
builder.Services.AddPersistence(builder.Configuration);

// ---------- Hyperliquid configuration ----------
builder.Services.AddOptions<HyperliquidOptions>()
    .Bind(builder.Configuration.GetSection(HyperliquidOptions.SectionName))
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

    pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(5));
});

builder.Services.AddSingleton<INonceProvider, NonceProvider>();
builder.Services.AddSingleton<IHyperliquidWebSocketClient, HyperliquidWebSocketClient>();

// ---------- Execution engine (signs + submits orders locally) ----------
builder.Services.AddSingleton<IExecutionEngine, LiveExecutionEngine>();

// ---------- Candle pipeline ----------
builder.Services.AddSingleton<MarketStateStore>();
builder.Services.AddSingleton<CandleClock>();
builder.Services.AddSingleton<CandleBuilder>();

// ---------- Strategy / trading pipeline ----------
builder.Services.AddSingleton<IMarketContextBuilder, LiveMarketContextBuilder>();
builder.Services.AddSingleton<IPositionManager, LivePositionManager>();
builder.Services.AddSingleton<GridStrategyEngine>();
builder.Services.AddSingleton<IConditionEvaluator, ConditionEvaluator>();
builder.Services.AddSingleton<ITrendFilterEvaluator, TrendFilterEvaluator>();
builder.Services.AddSingleton<IStrategyEngine, CompositeStrategyEngine>();
builder.Services.AddSingleton<IGridController, GridController>();
builder.Services.AddSingleton<ISignalController, SignalController>();

// ---------- Risk engine (live limits: daily loss, order size, circuit breaker) ----------
builder.Services.AddOptions<RiskLimitsConfig>()
    .Bind(builder.Configuration.GetSection(RiskLimitsConfig.SectionName));
builder.Services.AddSingleton<LiveRiskEngine>();
builder.Services.AddSingleton<IRiskEngine>(sp => sp.GetRequiredService<LiveRiskEngine>());

// ---------- Health monitoring ----------
builder.Services.AddSingleton<TradingHealthProvider>();
builder.Services.AddSingleton<ITradingHealthProvider>(sp => sp.GetRequiredService<TradingHealthProvider>());
builder.Services.AddHostedService<HealthMonitorService>();

// ---------- Agent check-in (polls API for commands, manages trading sessions) ----------
var agentConfig = builder.Configuration.GetSection(AgentOptions.SectionName).Get<AgentOptions>() ?? new AgentOptions();
builder.Services.AddHttpClient(AgentCheckInService.HttpClientName, client =>
{
    client.BaseAddress = new Uri(agentConfig.ControlPlaneUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHostedService<AgentCheckInService>();

var app = builder.Build();

// Configure wallet signer if key is available from config/env
if (!string.IsNullOrWhiteSpace(privateKey))
{
    app.Services.GetRequiredService<MutableSignerProvider>().Configure(privateKey);
}

await app.Services.MigrateDatabaseAsync();

app.Run();
