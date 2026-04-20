using System.Text;
using Microsoft.Extensions.Logging;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Infrastructure.Services;

namespace TradePilot.Api.Tests.Services;

[TestClass]
public sealed class BinanceFuturesAuthClientTests
{
    [TestMethod]
    public async Task GivenUnauthorizedFuturesCredential_WhenGetOpenOrdersAsync_ThenThrowsDomainExceptionInsteadOfAggregateException()
    {
        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"code\":-2015,\"msg\":\"Invalid API-key, IP, or permissions for action\"}", Encoding.UTF8, "application/json"),
        }));

        var act = () => client.GetOpenOrdersAsync();

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*enable Futures access*allow this machine's IP*");
    }

    [TestMethod]
    public async Task GivenUnauthorizedFuturesCredential_WhenGetBalancesAsync_ThenThrowsHelpfulDomainException()
    {
        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"code\":-2015,\"msg\":\"Invalid API-key, IP, or permissions for action\"}", Encoding.UTF8, "application/json"),
        }));

        var act = () => client.GetBalancesAsync();

        var exception = await act.Should().ThrowAsync<DomainException>();
        exception.Which.Message.Should().Contain("USD-M Futures");
        exception.Which.Message.Should().Contain("key/secret pair");
        exception.Which.Message.Should().Contain("selected environment");
    }

    private static BinanceFuturesAuthClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://fapi.binance.com"),
        };

        var logger = new Mock<ILogger<BinanceFuturesAuthClient>>();
        return new BinanceFuturesAuthClient(httpClient, logger.Object);
    }
}