using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Infrastructure.Hyperliquid.Models;
using TradingApp.Infrastructure.Services;

namespace TradingApp.Api.Tests.Services;

[TestClass]
public sealed class HyperliquidRestClientCandleSnapshotTests
{
    private const long StartTime = 1700000000000L;
    private const long EndTime = 1700001800000L;

    [TestMethod]
    public async Task GivenValidParams_WhenGetCandleSnapshotsAsync_ThenSendsRangeAndMapsAllCandles()
    {
        // Arrange
        var candles = new List<HyperliquidCandle>
        {
            new() { OpenTime = StartTime + 900000, Open = "50050", High = "50200", Low = "50000", Close = "50150", Volume = "90", NumTrades = 98 },
            new() { OpenTime = StartTime, Open = "50000", High = "50100", Low = "49900", Close = "50050", Volume = "100", NumTrades = 143 },
            new() { OpenTime = StartTime + 1800000, Open = "50150", High = "50300", Low = "50100", Close = "50250", Volume = "80", NumTrades = 77 },
        };

        var payload = JsonSerializer.Serialize(candles);
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        });

        var client = CreateClient(handler);

        // Act
        var result = await client.GetCandleSnapshotsAsync("BTC", "15m", StartTime, EndTime);

        // Assert
        result.Should().HaveCount(3);
        result.Select(c => c.Timestamp).Should().Equal(candles.Select(c => c.OpenTime));
        result.Select(c => c.NumTrades).Should().Equal(98, 143, 77);

        handler.LastRequestBody.Should().NotBeNullOrWhiteSpace();
        using var requestJson = JsonDocument.Parse(handler.LastRequestBody!);
        var req = requestJson.RootElement.GetProperty("req");
        req.GetProperty("coin").GetString().Should().Be("BTC");
        req.GetProperty("interval").GetString().Should().Be("15m");
        req.GetProperty("startTime").GetInt64().Should().Be(StartTime);
        req.GetProperty("endTime").GetInt64().Should().Be(EndTime);
    }

    [TestMethod]
    public async Task GivenInvalidTimeframe_WhenGetCandleSnapshotsAsync_ThenThrowsDomainException()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json"),
        });

        var client = CreateClient(handler);

        // Act
        Func<Task> act = () => client.GetCandleSnapshotsAsync("BTC", "invalid", StartTime, EndTime);

        // Assert
        await act.Should().ThrowAsync<DomainException>();
        handler.CallCount.Should().Be(0);
    }

    private static HyperliquidRestClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test.xyz"),
        };

        var logger = new Mock<ILogger<HyperliquidRestClient>>();
        return new HyperliquidRestClient(httpClient, logger.Object);
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public CapturingHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public int CallCount { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;

            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_response.StatusCode)
            {
                Content = _response.Content,
                RequestMessage = request,
            };
        }
    }
}
