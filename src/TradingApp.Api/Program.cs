using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using TradingApp.Api.Hubs;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Infrastructure.Filters;
using TradingApp.Api.Services;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Queries;
using TradingApp.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// MediatR - scan Application assembly for handlers
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<TradingApp.Application.Abstractions.Commands.Command>());

// AutoMapper - scan Application assembly for profiles
builder.Services.AddAutoMapper(typeof(GetMarketInfoQuery).Assembly);

// Identity stub (replace with real auth service in production)
builder.Services.AddSingleton<IdentityService>();

// Bind Hyperliquid configuration
builder.Services.AddOptions<HyperliquidOptions>()
    .Bind(builder.Configuration.GetSection(HyperliquidOptions.SectionName))
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
builder.Services.AddScoped<IHyperliquidOrderService, HyperliquidOrderService>();

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

builder.Services.AddControllers(options =>
{
    options.Filters.Add<HttpGlobalExceptionFilter>();
});

var app = builder.Build();

app.Logger.LogInformation(
    "Hyperliquid wallet configured: {WalletAddress}",
    signer.WalletAddress);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();
app.MapControllers();
app.MapHub<MarketDataHub>("/hubs/marketdata");

app.Run();

public partial class Program
{
}
