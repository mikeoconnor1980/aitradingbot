using TradingApp.Api.Infrastructure;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Infrastructure.Services;

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

// Read private key directly — consumed once at startup, not stored in DI
var privateKey = builder.Configuration
    .GetSection(HyperliquidOptions.SectionName)["PrivateKey"]
    ?? throw new InvalidOperationException(
        "Hyperliquid:PrivateKey is missing. Set 'Hyperliquid__PrivateKey' environment variable or add it to appsettings.Development.json.");

var signer = HyperliquidSigner.Create(privateKey);
builder.Services.AddSingleton<IHyperliquidSigner>(signer);

// Read non-secret config for BaseUrl, Network
var hyperliquidConfig = builder.Configuration
    .GetSection(HyperliquidOptions.SectionName)
    .Get<HyperliquidOptions>()
    ?? throw new InvalidOperationException(
        "Hyperliquid configuration section is missing. Add a 'Hyperliquid' section to appsettings.json or appsettings.Development.json.");

// Register HyperliquidRestClient as typed HttpClient with interface
builder.Services.AddHttpClient<IHyperliquidRestClient, HyperliquidRestClient>(client =>
{
    client.BaseAddress = new Uri(hyperliquidConfig.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

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
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

app.Logger.LogInformation(
    "Hyperliquid wallet configured: {WalletAddress} on {Network}",
    signer.WalletAddress,
    hyperliquidConfig.Network);

app.UseCors();
app.MapControllers();

app.Run();

public partial class Program
{
}
