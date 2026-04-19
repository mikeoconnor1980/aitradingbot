using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Infrastructure.Services;

namespace TradePilot.Infrastructure.Tests.Services;

[TestClass]
public sealed class HyperliquidWebSocketClientTests
{
    private readonly Mock<ILogger<HyperliquidWebSocketClient>> _loggerMock = new();
    private readonly Mock<IOptions<HyperliquidOptions>> _optionsMock = new();
    private readonly Mock<IHyperliquidRestClient> _restClientMock = new();

    [TestInitialize]
    public void Setup()
    {
        _optionsMock.Setup(o => o.Value).Returns(new HyperliquidOptions
        {
            WsBaseUrl = "wss://api.hyperliquid-testnet.xyz/ws",
        });

        var perpMetaJson = JsonSerializer.Deserialize<JsonElement>(
            """{"universe":[{"name":"BTC","maxLeverage":50},{"name":"ETH","maxLeverage":25}]}""");
        _restClientMock.Setup(r => r.PostInfoAsync<JsonElement>(
                It.Is<object>(o => IsInfoRequestType(o, "meta")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(perpMetaJson);

        var spotMetaJson = JsonSerializer.Deserialize<JsonElement>(
            """{"tokens":[{"name":"USDC","index":0},{"name":"IBTC","index":499}],"universe":[{"tokens":[499,0],"name":"@51","index":51,"isCanonical":false}]}""");
        _restClientMock.Setup(r => r.PostInfoAsync<JsonElement>(
                It.Is<object>(o => IsInfoRequestType(o, "spotMeta")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(spotMetaJson);
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

    [TestMethod]
    public async Task GivenSpotBaseCoin_WhenResolveSubscriptionCoinAsync_ThenReturnsSpotPairId()
    {
        var client = CreateClient();

        var result = await client.ResolveSubscriptionCoinAsync("IBTC");

        result.Should().Be("@51");
    }

    [TestMethod]
    public async Task GivenSpotPairId_WhenResolveDisplayAssetAsync_ThenReturnsUsdMarket()
    {
        var client = CreateClient();

        var result = await client.ResolveDisplayAssetAsync("@51");

        result.Should().Be("IBTC-USD");
    }

    private HyperliquidWebSocketClient CreateClient()
    {
        return new HyperliquidWebSocketClient(_loggerMock.Object, _optionsMock.Object, _restClientMock.Object);
    }

    private static bool IsInfoRequestType(object request, string expectedType)
    {
        var typeProperty = request.GetType().GetProperty("type");
        var typeValue = typeProperty?.GetValue(request)?.ToString();
        return string.Equals(typeValue, expectedType, StringComparison.OrdinalIgnoreCase);
    }
}