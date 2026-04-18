using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class RiskControllerTests
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private const string TestWalletAddress = "0xb63a3948477254cc17E0fb444050B9E161FCcFA3";

    private Mock<IHyperliquidAccountService> _accountServiceMock = null!;
    private Mock<IUserWalletAddressRepository> _walletRepoMock = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _accountServiceMock = new Mock<IHyperliquidAccountService>();
        _walletRepoMock = new Mock<IUserWalletAddressRepository>();

        _walletRepoMock
            .Setup(repo => repo.GetActiveByUserIdAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserWalletAddress.Create(TestUserId, TestWalletAddress));

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseInMemoryTradePilotPersistence($"risk-controller-tests-{Guid.NewGuid():N}");
                builder.UseSetting("Jwt:SecretKey", BaseControllerTests.TestJwtSecretKey);
                builder.UseSetting("Jwt:Issuer", "TradePilot");
                builder.UseSetting("Jwt:Audience", "TradePilot");
                builder.UseSetting("RiskLimits:MaxPortfolioHeatPercent", "6");
                builder.UseSetting("LlmReview:Provider", "Gemini");
                builder.UseSetting("LlmReview:BaseUrl", "https://example.test/openai/");
                builder.UseSetting("LlmReview:ModelName", "test-review-model");
                builder.UseSetting("LlmReview:ApiKey", "test-review-api-key");
                builder.UseSetting("LlmReview:TimeoutSeconds", "30");

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHyperliquidAccountService>();
                    services.AddSingleton(_accountServiceMock.Object);
                    services.RemoveAll<IUserWalletAddressRepository>();
                    services.AddSingleton(_walletRepoMock.Object);
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
    public async Task GivenOpenPositions_WhenGetPortfolioHeat_ThenReturnsHeatData()
    {
        _accountServiceMock
            .Setup(service => service.GetAccountSummaryAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountSummaryDto { Equity = 10_000m });
        _accountServiceMock
            .Setup(service => service.GetPositionsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PositionDto
                {
                    Asset = "BTC",
                    Size = 0.1m,
                    EntryPrice = 50_000m,
                    StopLossPrice = 49_000m,
                    MarginUsed = 500m,
                },
                new PositionDto
                {
                    Asset = "ETH",
                    Size = 1m,
                    EntryPrice = 3_000m,
                    MarginUsed = 300m,
                }
            ]);

        var response = await _client.GetAsync("api/risk/portfolio-heat");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PortfolioHeatResponse>();
        result.Should().NotBeNull();
        result!.HeatPercent.Should().Be(4m);
        result.MaxHeatPercent.Should().Be(6m);
        result.Equity.Should().Be(10_000m);
        result.Positions.Should().HaveCount(2);
        result.Positions.Should().ContainSingle(position => position.Symbol == "BTC" && position.RiskUsd == 100m);
        result.Positions.Should().ContainSingle(position => position.Symbol == "ETH" && position.RiskUsd == 300m);
    }

    [TestMethod]
    public async Task GivenNoWallet_WhenGetPortfolioHeat_ThenReturnsEmpty()
    {
        _walletRepoMock
            .Setup(repo => repo.GetActiveByUserIdAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserWalletAddress?)null);

        var response = await _client.GetAsync("api/risk/portfolio-heat");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PortfolioHeatResponse>();
        result.Should().NotBeNull();
        result!.HeatPercent.Should().Be(0m);
        result.MaxHeatPercent.Should().Be(6m);
        result.Positions.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenUnauthenticatedRequest_WhenGetPortfolioHeat_ThenReturnsUnauthorized()
    {
        using var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync("api/risk/portfolio-heat");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    private static string GenerateTestTokenWithGuid()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(BaseControllerTests.TestJwtSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "TradePilot",
            audience: "TradePilot",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Email, "test@tradepilot.dev"),
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim("token_type", "access"),
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}