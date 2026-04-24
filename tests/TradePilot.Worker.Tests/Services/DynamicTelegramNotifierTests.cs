using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

[TestClass]
public sealed class DynamicTelegramNotifierTests
{
    [TestMethod]
    public async Task GivenHtmlSensitiveContent_WhenNotifyingRiskEvent_ThenUsesHtmlParseModeAndEscapesContent()
    {
        var handler = new CapturingHandler();
        using var client = new HttpClient(handler);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var config = new NotificationConfigHolder
        {
            TelegramBotToken = "bot-token",
        };
        var logger = new Mock<ILogger<DynamicTelegramNotifier>>();

        httpClientFactory
            .Setup(factory => factory.CreateClient("TelegramBot"))
            .Returns(client);

        var notifier = new DynamicTelegramNotifier(httpClientFactory.Object, config, logger.Object);

        await notifier.NotifyRiskEventAsync(
            123456789,
            "Risk <Alert> & Review",
            "PnL < 0 & drawdown > 5",
            CancellationToken.None);

        handler.RequestBody.Should().NotBeNullOrWhiteSpace();

        using var json = JsonDocument.Parse(handler.RequestBody!);
        json.RootElement.GetProperty("parse_mode").GetString().Should().Be("HTML");

        var text = json.RootElement.GetProperty("text").GetString();
        text.Should().Contain("<b>Risk Alert</b>");
        text.Should().Contain("Risk &lt;Alert&gt; &amp; Review");
        text.Should().Contain("PnL &lt; 0 &amp; drawdown &gt; 5");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            };
        }
    }
}