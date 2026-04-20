using Microsoft.Extensions.Options;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Api.Tests.Services;

[TestClass]
public sealed class BinanceSigningHandlerTests
{
    [TestMethod]
    public async Task GivenUnsignedRequest_WhenSent_ThenAddsApiKeyTimestampRecvWindowAndSignature()
    {
        var innerHandler = new CapturingHandler();
        var handler = CreateHandler(innerHandler);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://fapi.binance.com"),
        };

        await client.GetAsync("/fapi/v2/account?symbol=BTCUSDT");

        innerHandler.LastRequest.Should().NotBeNull();
        innerHandler.LastRequest!.Headers.GetValues("X-MBX-APIKEY").Single().Should().Be("test-api-key");

        var query = innerHandler.LastRequest.RequestUri!.Query;
        query.Should().Contain("symbol=BTCUSDT");
        query.Should().Contain("timestamp=");
        query.Should().Contain("recvWindow=5000");
        query.Should().Contain("signature=");
    }

    [TestMethod]
    public async Task GivenRetryRequestWithExistingAuthParams_WhenSent_ThenReplacesInsteadOfDuplicating()
    {
        var innerHandler = new CapturingHandler();
        var handler = CreateHandler(innerHandler);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://fapi.binance.com"),
        };

        await client.GetAsync("/fapi/v2/account?symbol=BTCUSDT&timestamp=1&recvWindow=1&signature=old");

        var query = innerHandler.LastRequest!.RequestUri!.Query.TrimStart('?');
        query.Split('&').Count(part => part.StartsWith("timestamp=", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        query.Split('&').Count(part => part.StartsWith("recvWindow=", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        query.Split('&').Count(part => part.StartsWith("signature=", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
    }

    private static BinanceSigningHandler CreateHandler(HttpMessageHandler innerHandler)
    {
        return new BinanceSigningHandler(
        new StubExchangeCredentialAccessor(),
        Options.Create(new BinanceTradingOptions { BaseUrl = "https://fapi.binance.com", RecvWindowMs = 5000 }))
        {
            InnerHandler = innerHandler,
        };
    }

    private sealed class StubExchangeCredentialAccessor : IExchangeCredentialAccessor
    {
        public Task<ExchangeCredentialSnapshot?> GetActiveCredentialAsync(Exchange exchange, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ExchangeCredentialSnapshot?>(new ExchangeCredentialSnapshot(exchange, "test-api-key", "super-secret", "Test"));
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}