using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace TradingApp.Api.Tests.Infrastructure;

public abstract class BaseControllerTests
{
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
            JsonSerializer.Serialize(obj),
            Encoding.UTF8,
            "application/json");
    }
}

public static class HttpResponseExtensions
{
    public static async Task<T> ReadAndAssertSuccessAsync<T>(this HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<T>();
        content.Should().NotBeNull();
        return content!;
    }

    public static void AssertStatusCode(this HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.Should().Be(expected);
    }
}