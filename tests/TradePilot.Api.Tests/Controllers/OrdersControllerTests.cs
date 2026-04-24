using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradePilot.Api.Models;
using TradePilot.Api.Services;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Subscriptions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Domain.Subscriptions;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class OrdersControllerTests : BaseControllerTests
{
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";
    private static readonly Guid TestUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private Mock<IHyperliquidOrderService> _orderServiceMock = default!;
    private Mock<IHyperliquidAccountService> _accountServiceMock = default!;
    private Mock<IExchangeResolver> _exchangeResolverMock = default!;
    private Mock<ISubscriptionFeatureService> _subscriptionFeatureServiceMock = default!;
    private Mock<IBinanceExchangeInfoCache> _binanceExchangeInfoCacheMock = default!;
    private Mock<IHyperliquidRestClient> _hyperliquidRestClientMock = default!;
    private HttpClient _client = default!;

    [TestInitialize]
    public void Setup()
    {
        _orderServiceMock = new Mock<IHyperliquidOrderService>();
        _accountServiceMock = new Mock<IHyperliquidAccountService>();
        _exchangeResolverMock = new Mock<IExchangeResolver>();
        _subscriptionFeatureServiceMock = new Mock<ISubscriptionFeatureService>();
        _binanceExchangeInfoCacheMock = new Mock<IBinanceExchangeInfoCache>();
        _hyperliquidRestClientMock = new Mock<IHyperliquidRestClient>();
        _client = GetTestClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateTestToken(TestUserId.ToString()));

        _subscriptionFeatureServiceMock
            .Setup(service => service.GetAllowedAssetsAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["BTC", "ETH", "DOGE"]);
        _subscriptionFeatureServiceMock
            .Setup(service => service.GetPolicyAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierFeaturePolicy.ForTier(SubscriptionTier.Pro));
        _subscriptionFeatureServiceMock
            .Setup(service => service.IsAssetAllowed(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()))
            .Returns<IReadOnlyList<string>, string>((assets, market) =>
                assets.Any(asset => string.Equals(asset, market, StringComparison.OrdinalIgnoreCase)) ||
                assets.Any(asset => string.Equals(asset, market.Replace("-PERP", string.Empty, StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase)));

        _exchangeResolverMock
            .Setup(resolver => resolver.GetCurrentExchangeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Exchange.Hyperliquid);
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
        services.RemoveAll<IHyperliquidAccountService>();
        services.RemoveAll<IExchangeResolver>();
        services.RemoveAll<ISubscriptionFeatureService>();
        services.RemoveAll<IBinanceExchangeInfoCache>();
        services.RemoveAll<IHyperliquidRestClient>();
        services.AddSingleton(_orderServiceMock.Object);
        services.AddSingleton(_accountServiceMock.Object);
        services.AddSingleton(_exchangeResolverMock.Object);
        services.AddSingleton(_subscriptionFeatureServiceMock.Object);
        services.AddSingleton(_binanceExchangeInfoCacheMock.Object);
        services.AddSingleton(_hyperliquidRestClientMock.Object);
    }

    [TestMethod]
    public async Task GivenBinanceExchange_WhenGetAvailableAssetsAsync_ThenReturnsCanonicalAssetsFromUnifiedProvider()
    {
        _exchangeResolverMock
            .Setup(resolver => resolver.GetCurrentExchangeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Exchange.Binance);
        _binanceExchangeInfoCacheMock
            .Setup(cache => cache.GetSupportedSymbolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, BinanceExchangeSymbolMetadata>
            {
                ["BTC"] = new("BTC", "BTCUSDT", 3, 1, 125),
                ["DOGE"] = new("DOGE", "DOGEUSDT", 0, 5, 50),
            });

        var response = await _client.GetAsync("api/orders/assets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<TradableAssetDto>>();
        result.Should().NotBeNull();
        var assets = result!;
        assets.Select(asset => asset.Symbol).Should().Equal("BTC-PERP", "DOGE-PERP");
        assets[0].MaxLeverage.Should().Be(125);
        assets[0].SzDecimals.Should().Be(3);
    }

    [TestMethod]
    public async Task GivenHyperliquidExchange_WhenGetAvailableAssetsAsync_ThenReturnsCanonicalAssetsFromUnifiedProvider()
    {
        _exchangeResolverMock
            .Setup(resolver => resolver.GetCurrentExchangeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Exchange.Hyperliquid);
        _hyperliquidRestClientMock
            .Setup(client => client.PostInfoAsync<JsonElement>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse(
                """
                {
                  "universe": [
                    { "name": "ETH", "szDecimals": 4, "maxLeverage": 40 },
                    { "name": "BTC", "szDecimals": 5, "maxLeverage": 50 }
                  ]
                }
                """).RootElement.Clone());

        var response = await _client.GetAsync("api/orders/assets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<TradableAssetDto>>();
        result.Should().NotBeNull();
        var assets = result!;
        assets.Select(asset => asset.Symbol).Should().Equal("BTC-PERP", "ETH-PERP");
        assets[0].Name.Should().Be("Bitcoin");
        assets[0].SzDecimals.Should().Be(5);
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

    [TestMethod]
    public async Task GivenOpenOrder_WhenDeleteOrderById_ThenReturnsNoContent()
    {
        _accountServiceMock
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OpenOrderDto { OrderId = "12345", Asset = "BTC", Side = "Buy", Price = 60000m, Size = 0.01m },
            ]);

        _orderServiceMock
            .Setup(s => s.CancelOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.DeleteAsync("api/orders/12345");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        // POC: Controller resolves asset from open orders — currently always BTC. Update when multi-asset support is added.
        _orderServiceMock.Verify(
            s => s.CancelOrderAsync("12345", "BTC", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenOpenOrders_WhenDeleteOrdersByAsset_ThenReturnsNoContent()
    {
        _orderServiceMock
            .Setup(s => s.CancelAllOrdersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.DeleteAsync("api/orders?asset=BTC");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _orderServiceMock.Verify(
            s => s.CancelAllOrdersAsync("BTC", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenMissingAssetQuery_WhenDeleteOrdersByAsset_ThenReturnsBadRequest()
    {
        var response = await _client.DeleteAsync("api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenValidModifyRequest_WhenPutOrder_ThenReturnsNoContent()
    {
        _accountServiceMock
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OpenOrderDto { OrderId = "12345", Asset = "BTC", Side = "Buy", Price = 60000m, Size = 0.01m },
            ]);

        _orderServiceMock
            .Setup(s => s.ModifyOrderAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PutAsJsonAsync("api/orders/12345", new { price = 64500m, size = 0.002m });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _orderServiceMock.Verify(
            s => s.ModifyOrderAsync("12345", "BTC", "Buy", 64500m, 0.002m, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenInvalidModifyBody_WhenPutOrder_ThenReturnsBadRequest()
    {
        var response = await _client.PutAsJsonAsync("api/orders/12345", new { price = -1m, size = 0m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenOrderNotFound_WhenPutOrder_ThenReturnsNotFound()
    {
        _accountServiceMock
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _client.PutAsJsonAsync("api/orders/99999", new { price = 64500m, size = 0.002m });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenServiceUnavailable_WhenDeleteOrderById_ThenReturnsServiceUnavailable()
    {
        _accountServiceMock
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OpenOrderDto { OrderId = "12345", Asset = "BTC", Side = "Buy", Price = 60000m, Size = 0.01m },
            ]);

        _orderServiceMock
            .Setup(s => s.CancelOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var response = await _client.DeleteAsync("api/orders/12345");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [TestMethod]
    public async Task GivenValidTriggerRequest_WhenPostTriggerOrder_ThenReturnsOkWithResponse()
    {
        _orderServiceMock
            .Setup(s => s.PlaceTriggerOrderAsync(It.IsAny<PlaceTriggerOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaceOrderResponse
            {
                Success = true,
                OrderId = "55555",
                Status = "open",
            });

        var request = new PlaceTriggerOrderRequest
        {
            Asset = "BTC",
            Side = "sell",
            Size = 0.1m,
            TriggerPrice = 64000m,
            TpslType = "sl",
        };

        var response = await _client.PostAsJsonAsync("api/orders/trigger", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.OrderId.Should().Be("55555");
    }

    [TestMethod]
    public async Task GivenInvalidTriggerBody_WhenPostTriggerOrder_ThenReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("api/orders/trigger", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenValidTriggerModifyRequest_WhenPutTriggerOrder_ThenReturnsNoContent()
    {
        _accountServiceMock
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OpenOrderDto
                {
                    OrderId = "12345",
                    Asset = "BTC",
                    Side = "Sell",
                    OrderType = "trigger",
                    TpslType = "sl",
                    TriggerPrice = 64000m,
                    IsReduceOnly = true,
                },
            ]);

        _orderServiceMock
            .Setup(s => s.ModifyTriggerOrderAsync(
                "12345",
                "BTC",
                "Sell",
                64500m,
                0.002m,
                "sl",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PutAsJsonAsync("api/orders/trigger/12345", new { triggerPrice = 64500m, size = 0.002m });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [TestMethod]
    public async Task GivenExistingTriggerOrder_WhenDeleteTriggerOrder_ThenReturnsNoContent()
    {
        _accountServiceMock
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OpenOrderDto
                {
                    OrderId = "456",
                    Asset = "ETH",
                    Side = "Buy",
                    OrderType = "trigger",
                    TpslType = "tp",
                    TriggerPrice = 3200m,
                    IsReduceOnly = true,
                },
            ]);

        _orderServiceMock
            .Setup(s => s.CancelOrderAsync("456", "ETH", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.DeleteAsync("api/orders/trigger/456");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [TestMethod]
    public async Task GivenMissingTriggerOrder_WhenDeleteTriggerOrder_ThenReturnsNotFound()
    {
        _accountServiceMock
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _client.DeleteAsync("api/orders/trigger/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
