using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.MarketData.Models;
using TradePilot.Infrastructure.Services;

namespace TradePilot.Infrastructure.Tests.Services;

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
    public void GivenUserEventClientType_WhenCheckingReceiveBufferSize_ThenIs8192()
    {
        HyperliquidUserEventClient.ReceiveBufferSize.Should().Be(8192);
    }

    [TestMethod]
    public void GivenUserEventClientType_WhenCheckingConnectTimeout_ThenIs15Seconds()
    {
        HyperliquidUserEventClient.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(15));
    }

    [TestMethod]
    public async Task GivenMultipleStateHandlers_WhenOneRemoved_ThenOnlyRemainingHandlerIsInvoked()
    {
        var client = CreateClient();
        var firstCount = 0;
        var secondCount = 0;

        Func<WebSocketConnectionState, Task> firstHandler = _ =>
        {
            firstCount++;
            return Task.CompletedTask;
        };

        Func<WebSocketConnectionState, Task> secondHandler = _ =>
        {
            secondCount++;
            return Task.CompletedTask;
        };

        client.OnConnectionStateChanged(firstHandler);
        client.OnConnectionStateChanged(secondHandler);

        await InvokeStateChangeAsync(client, WebSocketConnectionState.Connected);

        client.RemoveConnectionStateChangedHandler(firstHandler);

        await InvokeStateChangeAsync(client, WebSocketConnectionState.Disconnected);

        firstCount.Should().Be(1);
        secondCount.Should().Be(2);
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

    private static async Task InvokeStateChangeAsync(HyperliquidUserEventClient client, WebSocketConnectionState state)
    {
        var method = typeof(HyperliquidUserEventClient).GetMethod(
            "NotifyStateChangeAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var task = (Task?)method!.Invoke(client, [state]);
        task.Should().NotBeNull();
        await task!;
    }
}
