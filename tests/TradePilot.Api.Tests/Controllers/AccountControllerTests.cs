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
using TradePilot.Api.Models;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class AccountControllerTests
{
    // Well-known Ethereum documentation example key — not a real credential
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private const string TestWalletAddress = "0xb63a3948477254cc17E0fb444050B9E161FCcFA3";

    private Mock<IExchangeAccountClient> _hyperliquidAccountClientMock = null!;
    private Mock<IExchangeAccountClient> _binanceAccountClientMock = null!;
    private Mock<IExchangeResolver> _exchangeResolverMock = null!;
    private Mock<IUserWalletAddressRepository> _walletRepoMock = null!;
    private Mock<IUserExchangeCredentialRepository> _credentialRepoMock = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _hyperliquidAccountClientMock = new Mock<IExchangeAccountClient>();
        _binanceAccountClientMock = new Mock<IExchangeAccountClient>();
        _exchangeResolverMock = new Mock<IExchangeResolver>();
        _walletRepoMock = new Mock<IUserWalletAddressRepository>();
        _credentialRepoMock = new Mock<IUserExchangeCredentialRepository>();

        _hyperliquidAccountClientMock.SetupGet(client => client.Exchange).Returns(Exchange.Hyperliquid);
        _binanceAccountClientMock.SetupGet(client => client.Exchange).Returns(Exchange.Binance);
        _exchangeResolverMock
            .Setup(resolver => resolver.GetCurrentExchangeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Exchange.Hyperliquid);

        // Return a wallet address for the test user
        _walletRepoMock
            .Setup(r => r.GetActiveByUserIdAndExchangeAsync(TestUserId, Exchange.Hyperliquid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserWalletAddress.Create(TestUserId, TestWalletAddress));

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseInMemoryTradePilotPersistence($"account-controller-tests-{Guid.NewGuid():N}");
                builder.UseSetting("Jwt:SecretKey", BaseControllerTests.TestJwtSecretKey);
                builder.UseSetting("Jwt:Issuer", "TradePilot");
                builder.UseSetting("Jwt:Audience", "TradePilot");
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
                    services.RemoveAll<IExchangeAccountClient>();
                    services.AddSingleton(_hyperliquidAccountClientMock.Object);
                    services.AddSingleton(_binanceAccountClientMock.Object);
                    services.RemoveAll<IExchangeResolver>();
                    services.AddSingleton(_exchangeResolverMock.Object);
                    services.RemoveAll<IUserWalletAddressRepository>();
                    services.AddSingleton(_walletRepoMock.Object);
                    services.RemoveAll<IUserExchangeCredentialRepository>();
                    services.AddSingleton(_credentialRepoMock.Object);
                });
            });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateTestTokenWithGuid());
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
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

        _hyperliquidAccountClientMock
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

        _hyperliquidAccountClientMock
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
        _hyperliquidAccountClientMock
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

        _hyperliquidAccountClientMock
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

        _hyperliquidAccountClientMock
            .Setup(s => s.GetRecentFillsAsync(
                TradingPair.Create("BTC", "USD", AssetType.Perp),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
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

        _hyperliquidAccountClientMock
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
        _hyperliquidAccountClientMock
            .Setup(s => s.GetRecentFillsAsync(
                TradingPair.Create("SOL", "USD", AssetType.Perp),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
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
        _hyperliquidAccountClientMock
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
        _hyperliquidAccountClientMock
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
        _hyperliquidAccountClientMock
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

    [TestMethod]
    public async Task GivenBinanceSelectedAndCredentialExists_WhenGetAccountSummary_ThenReturnsBinanceSummary()
    {
        var expected = new AccountSummaryDto
        {
            Equity = 2500m,
            AvailableMargin = 2000m,
            MaintenanceMargin = 50m,
            CrossMarginRatio = 0.02m,
            UnrealisedPnl = 25m,
        };

        _exchangeResolverMock
            .Setup(resolver => resolver.GetCurrentExchangeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Exchange.Binance);

        _credentialRepoMock
            .Setup(repository => repository.GetActiveByUserIdAndExchangeAsync(TestUserId, Exchange.Binance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserExchangeCredential.Create(TestUserId, Exchange.Binance, "api-key", "encrypted-secret", "Primary Binance"));

        _binanceAccountClientMock
            .Setup(client => client.GetAccountSummaryAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var response = await _client.GetAsync("api/account");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AccountSummaryDto>();
        result.Should().BeEquivalentTo(expected);
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

    private static string GenerateTestTokenWithGuid()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(BaseControllerTests.TestJwtSecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "TradePilot",
            audience: "TradePilot",
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Email, "test@tradepilot.dev"),
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim("token_type", "access"),
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}