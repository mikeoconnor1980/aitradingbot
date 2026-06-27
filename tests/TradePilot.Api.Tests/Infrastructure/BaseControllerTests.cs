using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using TradePilot.Persistence;

namespace TradePilot.Api.Tests.Infrastructure;

public abstract class BaseControllerTests
{
    // Test-only key — not a real credential
    internal const string TestJwtSecretKey = "test-secret-key-at-least-thirty-two-characters-long";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    // Shared InMemory DB root so all DbContext instances see the same data
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();

    private readonly string _testDatabaseName = $"api-tests-{Guid.NewGuid():N}";

    private WebApplicationFactory<Program>? _factory;

    /// <summary>Creates a DbContext connected to this test's InMemory database.</summary>
    protected TradePilotDbContext CreateTestDbContext()
    {
        var options = new DbContextOptionsBuilder<TradePilotDbContext>()
            .UseInMemoryDatabase(_testDatabaseName, DatabaseRoot)
            .Options;
        return new TradePilotDbContext(options);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _factory?.Dispose();
        _factory = null;
    }

    protected HttpClient GetTestClient(bool authenticate = true, string userId = "dev-user", string email = "test@tradepilot.dev", string displayName = "Test User")
    {
        _factory?.Dispose();
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Jwt:SecretKey", TestJwtSecretKey);
                builder.UseSetting("Jwt:Issuer", "TradePilot");
                builder.UseSetting("Jwt:Audience", "TradePilot");
                builder.UseSetting("LlmReview:Provider", "Gemini");
                builder.UseSetting("LlmReview:BaseUrl", "https://example.test/openai/");
                builder.UseSetting("LlmReview:ModelName", "test-review-model");
                builder.UseSetting("LlmReview:ApiKey", "test-review-api-key");
                builder.UseSetting("LlmReview:TimeoutSeconds", "30");
                builder.UseSetting("LlmContext:Provider", "Gemini");
                builder.UseSetting("LlmContext:BaseUrl", "https://example.test/openai/");
                builder.UseSetting("LlmContext:ModelName", "test-context-model");
                builder.UseSetting("LlmContext:ApiKey", "test-context-api-key");
                builder.UseSetting("LlmContext:TimeoutSeconds", "30");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=fake;Database=fake;");
                ConfigureWebHost(builder);
                builder.ConfigureServices(services =>
                {
                    // Replace SQL Server DbContext with InMemory for all tests
                    var efServiceProvider = new ServiceCollection()
                        .AddEntityFrameworkInMemoryDatabase()
                        .BuildServiceProvider();

                    services.RemoveAll<DbContextOptions<TradePilotDbContext>>();
                    services.AddSingleton<DbContextOptions<TradePilotDbContext>>(
                        new DbContextOptionsBuilder<TradePilotDbContext>()
                            .UseInMemoryDatabase(_testDatabaseName, DatabaseRoot)
                            .UseInternalServiceProvider(efServiceProvider)
                            .Options);

                    // Suppress all background services so tests don't make real HTTP calls
                    services.RemoveAll<IHostedService>();

                    ConfigureTestServices(services);
                });
            });

        var client = _factory.CreateClient();

        if (authenticate)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", GenerateTestToken(userId, email, displayName));
        }

        return client;
    }

    internal static string GenerateTestToken(
        string userId = "dev-user",
        string email = "test@tradepilot.dev",
        string displayName = "Test User")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "TradePilot",
            audience: "TradePilot",
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, displayName),
                new Claim("token_type", "access"),
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected virtual void ConfigureWebHost(IWebHostBuilder builder)
    {
    }

    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    protected static StringContent GetStringContent(object obj)
    {
        return new StringContent(
            JsonSerializer.Serialize(obj, JsonOptions),
            Encoding.UTF8,
            "application/json");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

        return options;
    }
}

public static class HttpResponseExtensions
{
    public static async Task<T> ReadAndAssertSuccessAsync<T>(this HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<T>(BaseControllerTestsJson.Options);
        content.Should().NotBeNull();
        return content!;
    }

    public static async Task<T> ReadAndAssertCreatedAsync<T>(this HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadFromJsonAsync<T>(BaseControllerTestsJson.Options);
        content.Should().NotBeNull();
        return content!;
    }

    public static void AssertStatusCode(this HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.Should().Be(expected);
    }
}

internal static class BaseControllerTestsJson
{
    internal static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

        return options;
    }
}