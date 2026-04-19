using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TradePilot.Api.Services;
using TradePilot.Application.Abstractions.Configuration;

namespace TradePilot.Api.Tests.Services;

[TestClass]
public sealed class TelegramBotPollingServiceTests
{
    [TestMethod]
    public async Task GivenPollingService_WhenEnsurePollingModeAsync_ThenCallsDeleteWebhook()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new CapturingHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true,\"result\":true}", Encoding.UTF8, "application/json"),
            };
        });

        var sut = CreateSut(handler);

        await sut.EnsurePollingModeAsync(CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.AbsoluteUri.Should().Be("https://api.telegram.org/bottest-token/deleteWebhook");
        var payload = await capturedRequest.Content!.ReadAsStringAsync();
        payload.Should().Contain("drop_pending_updates");
        payload.Should().Contain("false");
    }

    [TestMethod]
    public void GivenConflictException_WhenIsConflict_ThenReturnsTrue()
    {
        var exception = new HttpRequestException("conflict", null, HttpStatusCode.Conflict);

        var result = TelegramBotPollingService.IsConflict(exception);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void GivenNonConflictException_WhenIsConflict_ThenReturnsFalse()
    {
        var exception = new HttpRequestException("bad gateway", null, HttpStatusCode.BadGateway);

        var result = TelegramBotPollingService.IsConflict(exception);

        result.Should().BeFalse();
    }

    private static TelegramBotPollingService CreateSut(HttpMessageHandler handler)
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var logger = new Mock<ILogger<TelegramBotPollingService>>();
        var telegramOptions = Options.Create(new TelegramOptions
        {
            BotToken = "test-token",
        });
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory.Setup(factory => factory.CreateClient("TelegramBot"))
            .Returns(new HttpClient(handler));

        return new TelegramBotPollingService(
            scopeFactory.Object,
            telegramOptions,
            clientFactory.Object,
            logger.Object);
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}