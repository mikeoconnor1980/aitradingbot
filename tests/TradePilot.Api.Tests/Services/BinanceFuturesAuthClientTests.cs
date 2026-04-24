using System.Text;
using Microsoft.Extensions.Logging;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Infrastructure.Services;

namespace TradePilot.Api.Tests.Services;

[TestClass]
public sealed class BinanceFuturesAuthClientTests
{
    [TestMethod]
    public async Task GivenUnauthorizedFuturesCredential_WhenGetOpenOrdersAsync_ThenThrowsPermanentBinanceApiException()
    {
        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"code\":-2015,\"msg\":\"Invalid API-key, IP, or permissions for action\"}", Encoding.UTF8, "application/json"),
        }));

        var act = () => client.GetOpenOrdersAsync();

        var exception = await act.Should().ThrowAsync<BinanceApiException>();
        exception.Which.IsTransient.Should().BeFalse();
        exception.Which.BinanceErrorCode.Should().Be(-2015);
        exception.Which.Message.Should().Contain("enable Futures access");
        exception.Which.Message.Should().Contain("allow this machine's IP");
    }

    [TestMethod]
    public async Task GivenUnauthorizedFuturesCredential_WhenGetBalancesAsync_ThenThrowsHelpfulBinanceApiException()
    {
        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"code\":-2015,\"msg\":\"Invalid API-key, IP, or permissions for action\"}", Encoding.UTF8, "application/json"),
        }));

        var act = () => client.GetBalancesAsync();

        var exception = await act.Should().ThrowAsync<BinanceApiException>();
        exception.Which.Message.Should().Contain("USD-M Futures");
        exception.Which.Message.Should().Contain("key/secret pair");
        exception.Which.Message.Should().Contain("selected environment");
        exception.Which.IsTransient.Should().BeFalse();
    }

    [TestMethod]
    public async Task GivenMarginTypeAlreadySet_WhenSetMarginTypeAsync_ThenTreats4046AsSuccess()
    {
        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"code\":-4046,\"msg\":\"No need to change margin type.\"}", Encoding.UTF8, "application/json"),
        }));

        var act = () => client.SetMarginTypeAsync("BTCUSDT", isIsolated: true);

        await act.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task GivenForbiddenResponse_WhenGetOpenOrdersAsync_ThenThrowsPermanentBinanceApiException()
    {
        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"code\":-2015,\"msg\":\"API-key format invalid.\"}", Encoding.UTF8, "application/json"),
        }));

        var act = () => client.GetOpenOrdersAsync();

        var exception = await act.Should().ThrowAsync<BinanceApiException>();
        exception.Which.IsTransient.Should().BeFalse();
        exception.Which.BinanceErrorCode.Should().Be(-2015);
        exception.Which.Message.Should().Contain("forbidden");
    }

    [TestMethod]
    public async Task GivenServerError_WhenGetOpenOrdersAsync_ThenThrowsTransientBinanceApiException()
    {
        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{\"code\":-1000,\"msg\":\"Internal error\"}", Encoding.UTF8, "application/json"),
        }));

        var act = () => client.GetOpenOrdersAsync();

        var exception = await act.Should().ThrowAsync<BinanceApiException>();
        exception.Which.IsTransient.Should().BeTrue();
        exception.Which.BinanceErrorCode.Should().Be(-1000);
        exception.Which.Message.Should().Contain("server error 500");
    }

    [TestMethod]
    public async Task GivenInvalidQuantityError_WhenPlaceOrderAsync_ThenThrowsDomainExceptionContainingBinanceCode()
    {
        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"code\":-1111,\"msg\":\"Precision is over the maximum defined for this asset.\"}", Encoding.UTF8, "application/json"),
        }));

        var request = new BinancePlaceOrderRequest
        {
            Symbol = "BTCUSDT",
            Side = "BUY",
            Type = "MARKET",
            Quantity = 0.01m,
        };
        var act = () => client.PlaceOrderAsync(request);

        var exception = await act.Should().ThrowAsync<DomainException>();
        exception.Which.Message.Should().Contain("Invalid order quantity");
        exception.Which.Message.Should().Contain("Binance -1111");
    }

    [TestMethod]
    public async Task GivenInsufficientMarginError_WhenPlaceOrderAsync_ThenThrowsDomainExceptionContainingBinanceCode()
    {
        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"code\":-2019,\"msg\":\"Margin is insufficient.\"}", Encoding.UTF8, "application/json"),
        }));

        var request = new BinancePlaceOrderRequest
        {
            Symbol = "BTCUSDT",
            Side = "BUY",
            Type = "MARKET",
            Quantity = 0.01m,
        };
        var act = () => client.PlaceOrderAsync(request);

        var exception = await act.Should().ThrowAsync<DomainException>();
        exception.Which.Message.Should().Contain("Insufficient margin");
        exception.Which.Message.Should().Contain("Binance -2019");
    }

    [TestMethod]
    public async Task GivenBelowMinimumQuantityError_WhenPlaceOrderAsync_ThenThrowsDomainExceptionContainingBinanceCode()
    {
        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"code\":-4003,\"msg\":\"Quantity less than zero.\"}", Encoding.UTF8, "application/json"),
        }));

        var request = new BinancePlaceOrderRequest
        {
            Symbol = "BTCUSDT",
            Side = "BUY",
            Type = "MARKET",
            Quantity = 0.01m,
        };
        var act = () => client.PlaceOrderAsync(request);

        var exception = await act.Should().ThrowAsync<DomainException>();
        exception.Which.Message.Should().Contain("Quantity below minimum");
        exception.Which.Message.Should().Contain("Binance -4003");
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