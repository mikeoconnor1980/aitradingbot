using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Api.Tests.Hubs;

[TestClass]
public sealed class MarketDataHubTests
{
    private const string TestPrivateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [TestMethod]
    public async Task GivenSignalRHub_WhenClientConnects_ThenConnectionSucceeds()
    {
        var wsClientMock = new Mock<IHyperliquidWebSocketClient>();
        var restClientMock = new Mock<IHyperliquidRestClient>();
        var userEventClientMock = new Mock<IHyperliquidUserEventClient>();

        restClientMock
            .Setup(r => r.GetMarketInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketInfoDto?)null);

        wsClientMock
            .Setup(w => w.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        wsClientMock
            .Setup(w => w.SubscribeToTradesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        wsClientMock
            .Setup(w => w.ReceiveLoopAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => Task.Delay(Timeout.Infinite, ct));

        userEventClientMock
            .Setup(w => w.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        userEventClientMock
            .Setup(w => w.SubscribeToUserEventsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        userEventClientMock
            .Setup(w => w.ReceiveLoopAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => Task.Delay(Timeout.Infinite, ct));

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.UseInMemoryTradePilotPersistence($"market-data-hub-tests-{Guid.NewGuid():N}");
                builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
                builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
                builder.UseSetting("Hyperliquid:WsBaseUrl", "wss://api.hyperliquid-testnet.xyz/ws");
                builder.UseSetting("Hyperliquid:Network", "testnet");
                builder.UseSetting("LlmReview:Provider", "Gemini");
                builder.UseSetting("LlmReview:BaseUrl", "https://example.test/openai/");
                builder.UseSetting("LlmReview:ModelName", "test-review-model");
                builder.UseSetting("LlmReview:ApiKey", "test-review-api-key");
                builder.UseSetting("LlmReview:TimeoutSeconds", "30");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHyperliquidWebSocketClient>();
                    services.AddSingleton(wsClientMock.Object);

                    services.RemoveAll<IHyperliquidRestClient>();
                    services.AddSingleton(restClientMock.Object);

                    services.RemoveAll<IHyperliquidUserEventClient>();
                    services.AddSingleton(userEventClientMock.Object);
                });
            });

        var server = factory.Server;
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(
                $"{server.BaseAddress}hubs/marketdata",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();

        await hubConnection.StartAsync();

        try
        {
            hubConnection.State.Should().Be(HubConnectionState.Connected);
        }
        finally
        {
            await hubConnection.StopAsync();
            await hubConnection.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task GivenSignalRHub_WhenUserEventStreamRegistered_ThenConnectionStillSucceeds()
    {
        var wsClientMock = new Mock<IHyperliquidWebSocketClient>();
        var restClientMock = new Mock<IHyperliquidRestClient>();
        var userEventClientMock = new Mock<IHyperliquidUserEventClient>();

        restClientMock
            .Setup(r => r.GetMarketInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketInfoDto?)null);

        wsClientMock
            .Setup(w => w.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        wsClientMock
            .Setup(w => w.SubscribeToTradesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        wsClientMock
            .Setup(w => w.ReceiveLoopAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => Task.Delay(Timeout.Infinite, ct));

        userEventClientMock
            .Setup(w => w.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        userEventClientMock
            .Setup(w => w.SubscribeToUserEventsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        userEventClientMock
            .Setup(w => w.ReceiveLoopAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => Task.Delay(Timeout.Infinite, ct));

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.UseInMemoryTradePilotPersistence($"market-data-hub-tests-{Guid.NewGuid():N}");
                builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
                builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
                builder.UseSetting("Hyperliquid:WsBaseUrl", "wss://api.hyperliquid-testnet.xyz/ws");
                builder.UseSetting("Hyperliquid:Network", "testnet");
                builder.UseSetting("LlmReview:Provider", "Gemini");
                builder.UseSetting("LlmReview:BaseUrl", "https://example.test/openai/");
                builder.UseSetting("LlmReview:ModelName", "test-review-model");
                builder.UseSetting("LlmReview:ApiKey", "test-review-api-key");
                builder.UseSetting("LlmReview:TimeoutSeconds", "30");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHyperliquidWebSocketClient>();
                    services.AddSingleton(wsClientMock.Object);

                    services.RemoveAll<IHyperliquidRestClient>();
                    services.AddSingleton(restClientMock.Object);

                    services.RemoveAll<IHyperliquidUserEventClient>();
                    services.AddSingleton(userEventClientMock.Object);
                });
            });

        var server = factory.Server;
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(
                $"{server.BaseAddress}hubs/marketdata",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();

        await hubConnection.StartAsync();

        try
        {
            hubConnection.State.Should().Be(HubConnectionState.Connected);
        }
        finally
        {
            await hubConnection.StopAsync();
            await hubConnection.DisposeAsync();
        }
    }
}

