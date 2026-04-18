using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Polly;
using TradePilot.AI;
using TradePilot.Api.Hubs;
using TradePilot.Api.Infrastructure;
using TradePilot.Api.Infrastructure.Filters;
using TradePilot.Api.Services;
using TradePilot.Application.Abstractions.Auth;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Agent.Services;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.Backtesting;
using TradePilot.Application.Backtesting.Services;
using TradePilot.Application.MacroCalendar.Configuration;
using TradePilot.Application.MacroCalendar.Services;
using TradePilot.Application.MarketData.Queries;
using TradePilot.Application.Optimization;
using TradePilot.Application.Optimization.Services;
using TradePilot.Application.Scheduling;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.StrategyAuthoring.Validation;
using TradePilot.Application.Trading.Services;
using TradePilot.Application.Trading.Signals.Abstractions;
using TradePilot.Application.Trading.Signals.Implementations;
using TradePilot.Application.Trading.Signals.Registry;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;
using TradePilot.Infrastructure.Providers.MacroCalendar;
using TradePilot.Infrastructure.Services;
using TradePilot.Persistence;
using TradePilot.Persistence.Services;

var builder = WebApplication.CreateBuilder(args);

// MediatR - scan Application assembly for handlers
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<TradePilot.Application.Abstractions.Commands.Command>());



// Identity — resolved from JWT claims via HttpContext
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IdentityService>();
builder.Services.AddSingleton<IAdminAuthorizationService, ConfiguredEmailAdminAuthorizationService>();

builder.Services.AddOptions<AdminAuthorizationOptions>()
    .Bind(builder.Configuration.GetSection(AdminAuthorizationOptions.SectionName));

// JWT Authentication
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? new JwtOptions();

// Generate a dev key if none is configured
if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey))
{
    jwtOptions.SecretKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
}

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();

// Google Authentication
builder.Services.AddOptions<GoogleAuthOptions>()
    .Bind(builder.Configuration.GetSection(GoogleAuthOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();

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

// Bind FearGreed configuration
builder.Services.AddOptions<FearGreedOptions>()
    .Bind(builder.Configuration.GetSection(FearGreedOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RiskLimitsConfig>()
    .Bind(builder.Configuration.GetSection(RiskLimitsConfig.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.PostConfigure<RiskLimitsConfig>(options =>
{
    RiskLimitsConfigDefaults.Apply(options);
});
builder.Services.AddSingleton<IValidateOptions<RiskLimitsConfig>, RiskLimitsConfigValidator>();

// The control plane does not hold private keys.
// Wallet addresses are stored in the database; private keys live only on the execution agent (Worker).
var signerProvider = new MutableSignerProvider(
    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<MutableSignerProvider>());

builder.Services.AddSingleton(signerProvider);
builder.Services.AddSingleton<ISignerProvider>(sp => sp.GetRequiredService<MutableSignerProvider>());
builder.Services.AddSingleton<IHyperliquidSigner>(sp => sp.GetRequiredService<MutableSignerProvider>());

// Per-request network resolution from user profile
builder.Services.AddScoped<INetworkProvider, UserNetworkProvider>();
builder.Services.AddTransient<NetworkRoutingHandler>();

// Read non-secret config for BaseUrl, Network — use IOptions at resolution time
builder.Services.AddHttpClient<IHyperliquidRestClient, HyperliquidRestClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<HyperliquidOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30); // Outer timeout caps total retry duration
})
.AddHttpMessageHandler<NetworkRoutingHandler>()
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

builder.Services.AddScoped<IHyperliquidAccountService, TradePilot.Infrastructure.Services.HyperliquidAccountService>();
builder.Services.AddSingleton<INonceProvider, NonceProvider>();
builder.Services.AddSingleton<IHyperliquidAssetMetadataCache, HyperliquidAssetMetadataCache>();
builder.Services.AddScoped<ICandleIngestionService, CandleIngestionService>();
builder.Services.AddSingleton<AgentCommandStore>();
builder.Services.AddOptions<AgentUpdateOptions>()
    .Bind(builder.Configuration.GetSection(AgentUpdateOptions.SectionName));
builder.Services.AddOptions<InstallerOptions>()
    .Bind(builder.Configuration.GetSection(InstallerOptions.SectionName));
builder.Services.AddSingleton<BacktestExecutionContextAccessor>();
builder.Services.AddSingleton<BacktestJobQueue>();
builder.Services.AddSingleton<BacktestCancellationManager>();
builder.Services.AddSingleton<OptimizationJobQueue>();
builder.Services.AddSingleton<OptimizationCancellationRegistry>();
builder.Services.AddScoped<IMarketContextBuilder, BacktestMarketContextBuilder>();
builder.Services.AddScoped<GridStrategyEngine>();
builder.Services.AddScoped<DcaStrategyEngine>();
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
builder.Services.AddScoped<IConditionHandler, RsiConditionHandler>();
builder.Services.AddScoped<IConditionHandler, PriceVsEmaConditionHandler>();
builder.Services.AddScoped<IConditionHandler, MacdConditionHandler>();
builder.Services.AddScoped<IConditionHandler, SupportResistanceConditionHandler>();
builder.Services.AddScoped<IConditionHandler, DerivedSignalConditionHandler>();
builder.Services.AddScoped<ITrendFilterEvaluator, TrendFilterEvaluator>();
builder.Services.AddScoped<IConditionEvaluator, ConditionEvaluator>();
builder.Services.AddScoped<IStrategyEngine, CompositeStrategyEngine>();
builder.Services.AddScoped<IGridController, GridController>();
builder.Services.AddScoped<ISignalController, SignalController>();
builder.Services.AddScoped<IDcaController, DcaController>();
builder.Services.AddScoped<IRiskEngine, BacktestRiskEngine>();
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

// Live execution engine — wraps order service, implements IExecutionEngine for live trading
builder.Services.AddScoped<IExecutionEngine, HyperliquidExecutionEngine>();

// Candle builder pipeline — assembles confirmed candles from WebSocket trade stream
builder.Services.AddSingleton<MarketStateStore>();
builder.Services.AddSingleton<CandleClock>();
builder.Services.AddSingleton<CandleBuilder>();

// Macro calendar services
builder.Services.AddScoped<IMacroCalendarProvider, StubMacroCalendarProvider>();
builder.Services.AddScoped<IMacroBlockWindowCalculator, MacroBlockWindowCalculator>();
builder.Services.AddScoped<IMacroCalendarIngestionService, TradePilot.Persistence.Services.MacroCalendarIngestionService>();
builder.Services.AddScoped<IMacroCalendarQueryService, MacroCalendarQueryService>();
builder.Services.AddScoped<IMacroEventRiskCheck, MacroEventRiskCheck>();
builder.Services.AddHostedService<MacroCalendarSyncWorker>();
builder.Services.AddHostedService<ExecutionLogCleanupService>();

// Fear & Greed Index services
builder.Services.AddHttpClient<IFearGreedClient, FearGreedClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<FearGreedOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddResilienceHandler("fear-greed-retry", pipelineBuilder =>
{
    pipelineBuilder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(2),
        MaxDelay = TimeSpan.FromSeconds(30),
        UseJitter = true,
    });

    pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(15));
});
builder.Services.AddHostedService<FearGreedSyncWorker>();

// ---------- Telegram bot notifications ----------
builder.Services.AddOptions<TelegramOptions>()
    .Bind(builder.Configuration.GetSection(TelegramOptions.SectionName));

var telegramBotToken = builder.Configuration
    .GetSection(TelegramOptions.SectionName)["BotToken"];

if (!string.IsNullOrWhiteSpace(telegramBotToken))
{
    builder.Services.AddHttpClient<ITelegramNotifier, TelegramNotifier>((sp, client) =>
    {
        client.BaseAddress = new Uri($"https://api.telegram.org/bot{telegramBotToken}/");
        client.Timeout = TimeSpan.FromSeconds(10);
    });
}
else
{
    builder.Services.AddSingleton<ITelegramNotifier, NullTelegramNotifier>();
}

builder.Services.AddHttpClient("TelegramBot");
builder.Services.AddHostedService<TelegramBotPollingService>();

builder.Services.AddAI(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TradePilotDbContext>();

// SignalR — uses Azure SignalR Service when connection string is configured
var signalRConnectionString = builder.Configuration["Azure:SignalR:ConnectionString"];
if (!string.IsNullOrWhiteSpace(signalRConnectionString))
{
    builder.Services.AddSignalR().AddAzureSignalR(signalRConnectionString);
}
else
{
    builder.Services.AddSignalR();
}
builder.Services.AddSingleton<ISignalRPublisher, HubContextSignalRPublisher>();

// WebSocket client (singleton shared market data connection) — only needed when streaming locally (no Azure SignalR)
if (string.IsNullOrWhiteSpace(signalRConnectionString))
{
    builder.Services.AddSingleton<IHyperliquidWebSocketClient, HyperliquidWebSocketClient>();
    builder.Services.AddHostedService<MarketDataStreamService>();
    builder.Services.AddSingleton<IHyperliquidUserEventClient, HyperliquidUserEventClient>();
    builder.Services.AddHostedService<UserEventStreamService>();
}

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
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

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

// Refresh LLM context snapshot on every startup (development only)
if (app.Environment.IsDevelopment())
{
    using var seedScope = app.Services.CreateScope();
    var snapshotRepo = seedScope.ServiceProvider.GetRequiredService<ILlmContextSnapshotRepository>();
    var llmProvider = seedScope.ServiceProvider.GetService<ILlmContextProvider>();
    if (llmProvider is not null)
    {
        try
        {
            app.Logger.LogInformation("Calling LLM context provider for BTC startup seed...");
            var indicators = new IndicatorSnapshot { Rsi = 50m, Atr = 0m };
            var llmResult = await llmProvider.GetContextAsync("BTC", indicators);
            if (llmResult is not null)
            {
                var snapshot = LlmContextSnapshot.Create(
                    symbol: "BTC",
                    marketSentiment: llmResult.MarketSentiment,
                    macroRegime: llmResult.MacroRegime,
                    eventRisk: llmResult.EventRisk,
                    confidence: llmResult.Confidence,
                    derivedRegime: llmResult.DerivedRegime.ToString(),
                    summary: llmResult.Summary,
                    generatedAtUtc: llmResult.GeneratedAtUtc);
                await snapshotRepo.SaveAsync(snapshot);
                app.Logger.LogInformation(
                    "Seeded LLM context for BTC: {Regime}, {Sentiment}, confidence={Confidence:F2}",
                    llmResult.DerivedRegime, llmResult.MarketSentiment, llmResult.Confidence);
            }
            else
            {
                app.Logger.LogWarning("LLM context provider returned null for BTC; no snapshot seeded.");
            }
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Failed to seed LLM context snapshot on startup.");
        }
    }
    else
    {
        app.Logger.LogInformation("No LLM context provider configured; skipping context seed.");
    }
}

var configuredProvider = app.Services.GetRequiredService<ISignerProvider>();
if (configuredProvider.IsConfigured)
{
    app.Logger.LogInformation(
        "Hyperliquid wallet configured: {WalletAddress}",
        configuredProvider.WalletAddress);
}
else
{
    app.Logger.LogWarning(
        "No Hyperliquid wallet configured. Use the Profile page or set Hyperliquid__PrivateKey environment variable.");
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseForwardedHeaders();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<MarketDataHub>("/hubs/marketdata");
app.MapHealthChecks("/healthz");

app.Run();

public partial class Program
{
}
