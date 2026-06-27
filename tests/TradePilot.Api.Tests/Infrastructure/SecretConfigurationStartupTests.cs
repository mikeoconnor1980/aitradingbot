using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TradePilot.Api.Tests.Infrastructure;

[TestClass]
public sealed class SecretConfigurationStartupTests
{
    [TestMethod]
    public async Task GivenProductionEnvironmentWithoutJwtSecret_WhenApplicationStarts_ThenStartupFailsFast()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("Jwt:Issuer", "TradePilot");
                builder.UseSetting("Jwt:Audience", "TradePilot");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=fake;Database=fake;");
                builder.UseSetting("Llm:ApiKey", "test-api-key");
                builder.UseSetting("LlmReview:Provider", "Gemini");
                builder.UseSetting("LlmReview:BaseUrl", "https://example.test/openai/");
                builder.UseSetting("LlmReview:ModelName", "test-review-model");
                builder.UseSetting("LlmReview:ApiKey", "test-review-api-key");
                builder.UseSetting("LlmReview:TimeoutSeconds", "30");
                builder.UseSetting("LlmContext:ApiKey", "test-context-api-key");
            });

        Func<Task> act = async () =>
        {
            using var client = factory.CreateClient();
            await client.GetAsync("/api/version");
        };

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("Jwt:SecretKey");
    }
}