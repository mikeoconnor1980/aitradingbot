using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using TradingApp.AI;
using TradingApp.Api.Hubs;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Infrastructure.Filters;
using TradingApp.Api.Services;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.MacroCalendar.Configuration;
using TradingApp.Application.MacroCalendar.Services;
using TradingApp.Application.MarketData.Queries;
using TradingApp.Application.Optimization;
using TradingApp.Application.Optimization.Services;
using TradingApp.Application.StrategyAuthoring.Services;
using TradingApp.Application.StrategyAuthoring.Validation;
using TradingApp.Application.Trading.Services;
using TradingApp.Api.Services;
using TradingApp.Infrastructure.Providers.MacroCalendar;
using TradingApp.Infrastructure.Services;
using TradingApp.Persistence;
using TradingApp.Persistence.Services;

var builder = WebApplication.CreateBuilder(args);

// MediatR - scan Application assembly for handlers
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<TradingApp.Application.Abstractions.Commands.Command>());



// Identity stub (replace with real auth service in production)
builder.Services.AddSingleton<IdentityService>();

// Bind Hyperliquid configuration
builder.Services.AddOptions<HyperliquidOptions>()
    .Bind(builder.Configuration.GetSection(HyperliquidOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Bind CandleIngestion configuration
builder.Services.AddOptions<CandleIngestionOptions>()
    .Bind(builder.Configuration.GetSection(CandleIngestionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BinanceIngestionOptions>()
    .Bind(builder.Configuration.GetSection(BinanceIngestionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Bind MacroCalendar configuration
builder.Services.AddOptions<MacroCalendarOptions>()
    .Bind(builder.Configuration.GetSection(MacroCalendarOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Read private key directly — consumed once at startup, not stored in DI
var privateKey = builder.Configuration
    .GetSection(HyperliquidOptions.SectionName)["PrivateKey"]
    ?? throw new InvalidOperationException(
        "Hyperliquid:PrivateKey is missing. Set 'Hyperliquid__PrivateKey' environment variable or add it to appsettings.Development.json.");

var signer = HyperliquidSigner.Create(privateKey);
builder.Services.AddSingleton<IHyperliquidSigner>(signer);

// Read non-secret config for BaseUrl, Network — use IOptions at resolution time
builder.Services.AddHttpClient<IHyperliquidRestClient, HyperliquidRestClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<HyperliquidOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30); // Outer timeout caps total retry duration
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
            args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
            (args.Outcome.Result is not null && (int)args.Outcome.Result.StatusCode >= 500)),
        OnRetry = args =>
        {
            // Retry logging is handled by the Polly telemetry pipeline
            return ValueTask.CompletedTask;
        },
    });

    pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(5)); // Per-attempt timeout
});

builder.Services.AddScoped<IHyperliquidAccountService, HyperliquidAccountService>();
builder.Services.AddSingleton<INonceProvider, NonceProvider>();
builder.Services.AddSingleton<IHyperliquidAssetMetadataCache, HyperliquidAssetMetadataCache>();
builder.Services.AddScoped<ICandleIngestionService, CandleIngestionService>();
builder.Services.AddSingleton<BacktestExecutionContextAccessor>();
builder.Services.AddSingleton<BacktestJobQueue>();
builder.Services.AddSingleton<BacktestCancellationManager>();
builder.Services.AddSingleton<OptimizationJobQueue>();
builder.Services.AddSingleton<OptimizationCancellationRegistry>();
builder.Services.AddScoped<IMarketContextBuilder, BacktestMarketContextBuilder>();
builder.Services.AddScoped<GridStrategyEngine>();
builder.Services.AddScoped<IConditionHandler, RsiConditionHandler>();
builder.Services.AddScoped<IConditionHandler, PriceVsEmaConditionHandler>();
builder.Services.AddScoped<IConditionHandler, MacdConditionHandler>();
builder.Services.AddScoped<IConditionHandler, SupportResistanceConditionHandler>();
builder.Services.AddScoped<ITrendFilterEvaluator, TrendFilterEvaluator>();
builder.Services.AddScoped<IConditionEvaluator, ConditionEvaluator>();
builder.Services.AddScoped<IStrategyEngine, CompositeStrategyEngine>();
builder.Services.AddScoped<IGridController, GridController>();
builder.Services.AddScoped<ISignalController, SignalController>();
builder.Services.AddScoped<IRiskEngine, PassThroughRiskEngine>();
builder.Services.AddScoped<IPositionManager, BacktestPositionManager>();
builder.Services.AddScoped<IBacktestRunner, BacktestRunner>();
builder.Services.AddScoped<IStrategyConfigGenerator, StrategyConfigGenerator>();
builder.Services.AddScoped<IFitnessScorer, FitnessScorer>();
builder.Services.AddScoped<ISweepRunner, SweepRunner>();
builder.Services.AddSingleton<IChangeSummaryGenerator, ChangeSummaryGenerator>();
builder.Services.AddSingleton<IStrategyDiffService, StrategyDiffService>();
builder.Services.AddSingleton<SchemaValidator>();
builder.Services.AddSingleton<BusinessRuleValidator>();
builder.Services.AddSingleton<CrossFieldValidator>();
builder.Services.AddSingleton<IStrategyValidator, CompositeStrategyValidator>();
builder.Services.AddHostedService<BacktestProcessorService>();
builder.Services.AddHostedService<OptimizationProcessorService>();
builder.Services.AddHttpClient<IBinanceFuturesRestClient, BinanceFuturesRestClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<BinanceIngestionOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddResilienceHandler("binance-retry", pipelineBuilder =>
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
        OnRetry = args =>
        {
            return ValueTask.CompletedTask;
        },
    });

    pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(5));
});
builder.Services.AddScoped<IBinanceCandleIngestionService, BinanceCandleIngestionService>();
builder.Services.AddScoped<IFundingRateIngestionService, FundingRateIngestionService>();
builder.Services.AddScoped<IHyperliquidOrderService, HyperliquidOrderService>();

// Macro calendar services
builder.Services.AddScoped<IMacroCalendarProvider, StubMacroCalendarProvider>();
builder.Services.AddScoped<IMacroBlockWindowCalculator, MacroBlockWindowCalculator>();
builder.Services.AddScoped<IMacroCalendarIngestionService, TradingApp.Persistence.Services.MacroCalendarIngestionService>();
builder.Services.AddScoped<IMacroCalendarQueryService, MacroCalendarQueryService>();
builder.Services.AddScoped<IMacroEventRiskCheck, MacroEventRiskCheck>();
builder.Services.AddHostedService<MacroCalendarSyncWorker>();

builder.Services.AddAI(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);

// SignalR
builder.Services.AddSignalR();

// WebSocket client (singleton shared market data connection)
builder.Services.AddSingleton<IHyperliquidWebSocketClient, HyperliquidWebSocketClient>();

// Background service for market data streaming
builder.Services.AddHostedService<MarketDataStreamService>();

// User event WebSocket — separate connection for per-wallet subscriptions
builder.Services.AddSingleton<IHyperliquidUserEventClient, HyperliquidUserEventClient>();
builder.Services.AddHostedService<UserEventStreamService>();

// CORS
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("interpret-strategy", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString();

        if (partitionKey is null)
        {
            return RateLimitPartition.GetNoLimiter("unknown");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
    });

    options.AddPolicy("review-strategy", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers["Retry-After"] =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new Envelope("Too many requests. Please wait a moment.", "rate_limit"),
            cancellationToken);
    };
});

builder.Services.AddControllers(options =>
{
    options.Filters.Add<HttpGlobalExceptionFilter>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower));
});

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();

app.Logger.LogInformation(
    "Hyperliquid wallet configured: {WalletAddress}",
    signer.WalletAddress);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseForwardedHeaders();
app.UseCors();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<MarketDataHub>("/hubs/marketdata");

app.Run();

public partial class Program
{
}
