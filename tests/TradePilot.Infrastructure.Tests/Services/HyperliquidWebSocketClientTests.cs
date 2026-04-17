using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Infrastructure.Services;

namespace TradePilot.Infrastructure.Tests.Services;

[TestClass]
public sealed class HyperliquidWebSocketClientTests
{
    private readonly Mock<ILogger<HyperliquidWebSocketClient>> _loggerMock = new();
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
        var client = CreateClient();

        client.IsConnected.Should().BeFalse();
    }

    [TestMethod]
    public void GivenClient_WhenOnTradeReceivedRegistered_ThenDoesNotThrow()
    {
        var client = CreateClient();

        var act = () => client.OnTradeReceived(_ => Task.CompletedTask);

        act.Should().NotThrow();
    }

    [TestMethod]
    public void GivenClient_WhenOnConnectionStateChangedRegistered_ThenDoesNotThrow()
    {
        var client = CreateClient();

        var act = () => client.OnConnectionStateChanged(_ => Task.CompletedTask);

        act.Should().NotThrow();
    }

    [TestMethod]
    public async Task GivenNotConnected_WhenSubscribeToTrades_ThenThrowsInvalidOperationException()
    {
        var client = CreateClient();

        var act = () => client.SubscribeToTradesAsync("BTC");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not connected*");
    }

    private HyperliquidWebSocketClient CreateClient()
    {
        return new HyperliquidWebSocketClient(_loggerMock.Object, _optionsMock.Object);
    }
}