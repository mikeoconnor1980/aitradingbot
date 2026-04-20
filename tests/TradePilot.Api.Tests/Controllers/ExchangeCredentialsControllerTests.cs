using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Controllers;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class ExchangeCredentialsControllerTests
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [TestMethod]
    public async Task GivenStoredCredential_WhenGet_ThenSecretUsesFixedPlaceholder()
    {
        var repository = new Mock<IUserExchangeCredentialRepository>();
        repository
            .Setup(repo => repo.GetAllActiveByUserIdAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                UserExchangeCredential.Create(TestUserId, Exchange.Binance, "binance-key", "ciphertext-ending-in-16ik", "Primary Binance"),
            ]);

        var controller = new ExchangeCredentialsController(
            repository.Object,
            Mock.Of<ICredentialEncryptionService>(),
            Mock.Of<IBinanceFuturesAuthClient>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                    ],
                    authenticationType: "Test")),
                },
            },
        };

        var result = await controller.Get(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeAssignableTo<IReadOnlyList<ExchangeCredentialResponse>>().Subject;
        payload.Should().ContainSingle();
        payload[0].MaskedSecret.Should().Be("********");
    }
}