using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TradingApp.Api.Models;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;

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
                builder.UseSetting("Jwt:SecretKey", BaseControllerTests.TestJwtSecretKey);
                builder.UseSetting("Jwt:Issuer", "TradingApp");
                builder.UseSetting("Jwt:Audience", "TradingApp");
                builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
                builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
                builder.UseSetting("Hyperliquid:Network", "testnet");
                builder.UseSetting("LlmReview:Provider", "Gemini");
                builder.UseSetting("LlmReview:BaseUrl", "https://example.test/openai/");
                builder.UseSetting("LlmReview:ModelName", "test-review-model");
                builder.UseSetting("LlmReview:ApiKey", "test-review-api-key");
                builder.UseSetting("LlmReview:TimeoutSeconds", "30");

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHyperliquidAccountService>();
                    services.AddSingleton(_accountServiceMock.Object);
                });
            });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", BaseControllerTests.GenerateTestToken());
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
            .Setup(s => s.GetAccountSummaryAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.GetPositionsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
    public async Task GivenRecentFills_WhenGetFills_ThenReturnsSemanticFillFields()
    {
        // Arrange
        IReadOnlyList<FillEventDto> fills = new List<FillEventDto>
        {
            new()
            {
                Timestamp = new DateTime(2026, 3, 26, 20, 17, 19, DateTimeKind.Utc),
                Asset = "SUI",
                Side = "Buy",
                Direction = "Open Long",
                Size = 100m,
                Price = 0.92845m,
                Fee = 0.01m,
                ClosedPnl = 0m,
                OrderId = "12345"
            },
            new()
            {
                Timestamp = new DateTime(2026, 3, 26, 20, 16, 53, DateTimeKind.Utc),
                Asset = "ADA",
                Side = "Sell",
                Direction = "Close Long",
                Size = 50m,
                Price = 0.25537m,
                Fee = 0.01m,
                ClosedPnl = 12.34m,
                OrderId = "67890"
            }
        };

        _accountServiceMock
            .Setup(s => s.GetRecentFillsAsync(null, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fills);

        // Act
        var response = await _client.GetAsync("api/account/fills");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<FillEventDto>>();
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(fills);
    }

    [TestMethod]
    public async Task GivenFillsForMultipleAssets_WhenGetFillsWithAssetFilter_ThenReturnsOnlyMatchingFills()
    {
        // Arrange
        IReadOnlyList<FillEventDto> filteredFills = new List<FillEventDto>
        {
            CreateFill("BTC", "Buy", "Open Long", 0.1m, 65000m, 0.01m, 0m, "order-1"),
            CreateFill("BTC", "Sell", "Close Long", 0.1m, 66000m, 0.01m, 100m, "order-3"),
        };

        _accountServiceMock
            .Setup(s => s.GetRecentFillsAsync("BTC-PERP", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(filteredFills);

        // Act
        var response = await _client.GetAsync("api/account/fills?asset=BTC-PERP");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<FillEventDto>>();
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(fill => fill.Asset == "BTC");
    }

    [TestMethod]
    public async Task GivenFillsForMultipleAssets_WhenGetFillsWithoutAssetFilter_ThenReturnsAllFills()
    {
        // Arrange
        IReadOnlyList<FillEventDto> fills = new List<FillEventDto>
        {
            CreateFill("BTC", "Buy", "Open Long", 0.1m, 65000m, 0.01m, 0m, "order-1"),
            CreateFill("ETH", "Sell", "Close Long", 1m, 3200m, 0.02m, 50m, "order-2"),
        };

        _accountServiceMock
            .Setup(s => s.GetRecentFillsAsync(null, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fills);

        // Act
        var response = await _client.GetAsync("api/account/fills");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<FillEventDto>>();
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(fills);
    }

    [TestMethod]
    public async Task GivenNoFillsForAsset_WhenGetFillsWithAssetFilter_ThenReturnsEmptyList()
    {
        // Arrange
        _accountServiceMock
            .Setup(s => s.GetRecentFillsAsync("SOL-PERP", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FillEventDto>());

        // Act
        var response = await _client.GetAsync("api/account/fills?asset=SOL-PERP");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<FillEventDto>>();
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenHyperliquidUnavailable_WhenGetAccountSummary_ThenReturns503()
    {
        // Arrange
        _accountServiceMock
            .Setup(s => s.GetAccountSummaryAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.GetPositionsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var response = await _client.GetAsync("api/account/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Be("External service unavailable");
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    private static FillEventDto CreateFill(
        string asset,
        string side,
        string direction,
        decimal size,
        decimal price,
        decimal fee,
        decimal closedPnl,
        string orderId)
    {
        return new FillEventDto
        {
            Timestamp = new DateTime(2026, 3, 30, 12, 0, 0, DateTimeKind.Utc),
            Asset = asset,
            Side = side,
            Direction = direction,
            Size = size,
            Price = price,
            Fee = fee,
            ClosedPnl = closedPnl,
            OrderId = orderId,
        };
    }
}