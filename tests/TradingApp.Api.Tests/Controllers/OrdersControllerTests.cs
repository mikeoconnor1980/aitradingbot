using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradingApp.Api.Models;
using TradingApp.Api.Services;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Exceptions;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class OrdersControllerTests : BaseControllerTests
{
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    private Mock<IHyperliquidOrderService> _orderServiceMock = default!;
    private HttpClient _client = default!;

    [TestInitialize]
    public void Setup()
    {
        _orderServiceMock = new Mock<IHyperliquidOrderService>();
        _client = GetTestClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:WsBaseUrl", "wss://api.hyperliquid-testnet.xyz/ws");
        builder.UseSetting("Hyperliquid:Network", "testnet");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IHyperliquidOrderService>();
        services.AddSingleton(_orderServiceMock.Object);
    }

    [TestMethod]
    public async Task GivenValidLimitOrder_WhenPostOrders_ThenReturnsOkWithResponse()
    {
        _orderServiceMock
            .Setup(s => s.PlaceOrderAsync(It.IsAny<PlaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaceOrderResponse
            {
                Success = true,
                OrderId = "12345",
                Status = "open",
            });

        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP",
            Side = "buy",
            OrderType = "limit",
            Price = 65000m,
            Size = 0.001m,
        };

        var response = await _client.PostAsJsonAsync("api/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.OrderId.Should().Be("12345");
    }

    [TestMethod]
    public async Task GivenInvalidBody_WhenPostOrders_ThenReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("api/orders", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenTestSign_WhenPostTestSign_ThenReturnsOkWithSignature()
    {
        _orderServiceMock
            .Setup(s => s.TestSignAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSignResponse
            {
                DomainSeparator = "0xabc",
                TypeHash = "0xdef",
                MessageHash = "0x123",
                Signature = new SignatureDto
                {
                    V = 27,
                    R = "0xr",
                    S = "0xs",
                },
            });

        var response = await _client.PostAsync("api/orders/test-sign", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TestSignResponse>();
        result.Should().NotBeNull();
        result!.Signature.V.Should().Be(27);
    }

    [TestMethod]
    public async Task GivenDomainException_WhenPostOrders_ThenReturnsBadRequest()
    {
        _orderServiceMock
            .Setup(s => s.PlaceOrderAsync(It.IsAny<PlaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Invalid order"));

        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP",
            Side = "buy",
            OrderType = "limit",
            Price = 65000m,
            Size = 0.001m,
        };

        var response = await _client.PostAsJsonAsync("api/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
