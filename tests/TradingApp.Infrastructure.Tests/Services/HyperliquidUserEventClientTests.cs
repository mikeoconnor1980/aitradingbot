using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.MarketData.Models;
using TradingApp.Infrastructure.Services;

namespace TradingApp.Infrastructure.Tests.Services;

[TestClass]
public sealed class HyperliquidUserEventClientTests
{
    private readonly Mock<ILogger<HyperliquidUserEventClient>> _loggerMock = new();
    private readonly Mock<IOptions<HyperliquidOptions>> _optionsMock = new();

    [TestInitialize]
    public void Setup()
    {
        _optionsMock.Setup(o => o.Value).Returns(new HyperliquidOptions
        {
            WsBaseUrl = "wss://api.hyperliquid-testnet.xyz/ws",
        });
    }

    [TestMethod]
    public void GivenNewClient_WhenCreated_ThenIsConnectedIsFalse()
    {
        // Arrange & Act
        var client = CreateClient();

        // Assert
        client.IsConnected.Should().BeFalse();
    }

    [TestMethod]
    public void GivenClient_WhenOnFillReceivedRegistered_ThenDoesNotThrow()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var act = () => client.OnFillReceived(_ => Task.CompletedTask);

        // Assert
        act.Should().NotThrow();
    }

    [TestMethod]
    public void GivenClient_WhenOnOrderUpdateReceivedRegistered_ThenDoesNotThrow()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var act = () => client.OnOrderUpdateReceived(_ => Task.CompletedTask);

        // Assert
        act.Should().NotThrow();
    }

    [TestMethod]
    public void GivenClient_WhenOnConnectionStateChangedRegistered_ThenDoesNotThrow()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var act = () => client.OnConnectionStateChanged(_ => Task.CompletedTask);

        // Assert
        act.Should().NotThrow();
    }

    [TestMethod]
    public async Task GivenNotConnected_WhenSubscribeToUserEvents_ThenThrowsInvalidOperationException()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var act = () => client.SubscribeToUserEventsAsync("0x1234567890abcdef");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not connected*");
    }

    private HyperliquidUserEventClient CreateClient()
    {
        return new HyperliquidUserEventClient(_loggerMock.Object, _optionsMock.Object);
    }
}
