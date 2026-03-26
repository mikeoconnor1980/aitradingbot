using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradingApp.Api.Models;
using TradingApp.Api.Services;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class AccountControllerTests
{
    // Well-known Ethereum documentation example key — not a real credential
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    private Mock<IHyperliquidAccountService> _accountServiceMock = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _accountServiceMock = new Mock<IHyperliquidAccountService>();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
                builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
                builder.UseSetting("Hyperliquid:Network", "testnet");

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHyperliquidAccountService>();
                    services.AddSingleton(_accountServiceMock.Object);
                });
            });

        _client = _factory.CreateClient();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task GivenValidAccount_WhenGetAccountSummary_ThenReturnsOkWithSummary()
    {
        // Arrange
        var expected = new AccountSummaryDto
        {
            Equity = 10000m,
            AvailableMargin = 8000m,
            CrossMarginRatio = 0.05m,
            MaintenanceMargin = 500m,
            UnrealisedPnl = 150m,
        };

        _accountServiceMock
            .Setup(s => s.GetAccountSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var response = await _client.GetAsync("api/account");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AccountSummaryDto>();
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public async Task GivenPositionsExist_WhenGetPositions_ThenReturnsOkWithPositions()
    {
        // Arrange
        var positions = new List<PositionDto>
        {
            new()
            {
                Asset = "BTC",
                Size = 0.1m,
                Side = "Long",
                EntryPrice = 60000m,
                MarkPrice = 61000m,
                UnrealisedPnl = 100m,
                UnrealisedPnlPercent = 1.67m,
                LiquidationPrice = 55000m,
                Leverage = 5,
                MarginMode = "cross",
                MarginUsed = 1220m,
                FundingRate = -0.0001m,
            },
        };

        _accountServiceMock
            .Setup(s => s.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(positions);

        // Act
        var response = await _client.GetAsync("api/account/positions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<PositionDto>>();
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(positions);
    }

    [TestMethod]
    public async Task GivenNoOpenOrders_WhenGetOrders_ThenReturnsOkWithEmptyArray()
    {
        // Arrange
        _accountServiceMock
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenOrderDto>());

        // Act
        var response = await _client.GetAsync("api/account/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<OpenOrderDto>>();
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenHyperliquidUnavailable_WhenGetAccountSummary_ThenReturns503()
    {
        // Arrange
        _accountServiceMock
            .Setup(s => s.GetAccountSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var response = await _client.GetAsync("api/account");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Be("External service unavailable");
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenHyperliquidUnavailable_WhenGetPositions_ThenReturns503()
    {
        // Arrange
        _accountServiceMock
            .Setup(s => s.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var response = await _client.GetAsync("api/account/positions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Be("External service unavailable");
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenHyperliquidUnavailable_WhenGetOrders_ThenReturns503()
    {
        // Arrange
        _accountServiceMock
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var response = await _client.GetAsync("api/account/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Be("External service unavailable");
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }
}