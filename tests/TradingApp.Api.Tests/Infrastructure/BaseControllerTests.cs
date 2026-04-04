using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace TradingApp.Api.Tests.Infrastructure;

public abstract class BaseControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private WebApplicationFactory<Program>? _factory;

    [TestCleanup]
    public void Cleanup()
    {
        _factory?.Dispose();
        _factory = null;
    }

    protected HttpClient GetTestClient()
    {
        _factory?.Dispose();
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("LlmReview:Provider", "Gemini");
                builder.UseSetting("LlmReview:BaseUrl", "https://example.test/openai/");
                builder.UseSetting("LlmReview:ModelName", "test-review-model");
                builder.UseSetting("LlmReview:ApiKey", "test-review-api-key");
                builder.UseSetting("LlmReview:TimeoutSeconds", "30");
                ConfigureWebHost(builder);
                builder.ConfigureServices(services =>
                {
                    ConfigureTestServices(services);
                });
            });

        return _factory.CreateClient();
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