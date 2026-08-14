using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TradePilot.AI.Analyst;
using TradePilot.AI.Services;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.AI;

public static class AiServiceExtensions
{
    public static IServiceCollection AddAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<LlmOptions>()
            .Bind(configuration.GetSection(LlmOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<LlmReviewOptions>()
            .Bind(configuration.GetSection(LlmReviewOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<LlmContextOptions>()
            .Bind(configuration.GetSection(LlmContextOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<LlmAnalystOptions>()
            .Bind(configuration.GetSection(LlmAnalystOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddHttpClient<ILlmClient, OpenAiCompatibleLlmClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<LlmOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            ConfigureAuthentication(client, options.Provider, options.ApiKey);
        });

        services.AddHttpClient<IReviewLlmClient, ReviewLlmClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<LlmReviewOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            ConfigureAuthentication(client, options.Provider, options.ApiKey);
        });

        services.AddHttpClient<ILlmContextClient, LlmContextClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<LlmContextOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            ConfigureAuthentication(client, options.Provider, options.ApiKey);
        });

        services.AddHttpClient<IAnalystLlmClient, OpenAiCompatibleAnalystLlmClient>((serviceProvider, client) =>
        {
            // The Analyst intentionally reuses the existing general LLM provider configuration.
            var options = serviceProvider.GetRequiredService<IOptions<LlmOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            ConfigureAuthentication(client, options.Provider, options.ApiKey);
        });

        services.AddScoped<IStrategyInterpreter, StrategyInterpreter>();
        services.AddScoped<IStrategyReviewer, StrategyReviewer>();
        services.AddSingleton<ILlmContextProvider, LlmContextProvider>();
        services.AddScoped<IAnalystToolCatalog, TradePilotAnalystToolCatalog>();
        services.AddScoped<ITradingAnalyst, TradingAnalyst>();

        return services;
    }

    private static void ConfigureAuthentication(HttpClient client, string provider, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        if (AzureOpenAiResponsesProtocol.IsAzureOpenAi(provider))
        {
            client.DefaultRequestHeaders.Add("api-key", apiKey);
            return;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }
}
