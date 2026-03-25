using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.EIP712;
using TradingApp.Api.Models;
using TradingApp.Api.Services;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Infrastructure.Hyperliquid.Models;

namespace TradingApp.Api.Tests.Services;

[TestClass]
public sealed class HyperliquidOrderServiceTests
{
    private Mock<IHyperliquidRestClient> _restClientMock = default!;
    private Mock<IHyperliquidSigner> _signerMock = default!;
    private Mock<INonceProvider> _nonceProviderMock = default!;
    private Mock<ILogger<HyperliquidOrderService>> _loggerMock = default!;
    private IOptions<HyperliquidOptions> _options = default!;
    private HyperliquidOrderService _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _restClientMock = new Mock<IHyperliquidRestClient>();
        _signerMock = new Mock<IHyperliquidSigner>();
        _nonceProviderMock = new Mock<INonceProvider>();
        _loggerMock = new Mock<ILogger<HyperliquidOrderService>>();
        _options = Options.Create(new HyperliquidOptions
        {
            BaseUrl = "https://api.hyperliquid-testnet.xyz",
            WsBaseUrl = "wss://api.hyperliquid-testnet.xyz/ws",
            Network = "testnet",
        });

        _signerMock.Setup(s => s.WalletAddress).Returns("0xTestAddress");
        _signerMock
            .Setup(s => s.SignTypedData(It.IsAny<TypedData<Domain>>()))
            .Returns(("0x" + new string('a', 64), "0x" + new string('b', 64), 27));
        _nonceProviderMock
            .Setup(n => n.GetNextNonce())
            .Returns(1716499200000L);

        _sut = new HyperliquidOrderService(
            _restClientMock.Object,
            _signerMock.Object,
            _nonceProviderMock.Object,
            _options,
            _loggerMock.Object);
    }

    [TestMethod]
    public async Task GivenValidLimitOrder_WhenPlaceOrderAsync_ThenReturnsSuccessWithOrderId()
    {
        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP",
            Side = "buy",
            OrderType = "limit",
            Price = 65000m,
            Size = 0.001m,
        };

        _restClientMock
            .Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HyperliquidExchangeResponse
            {
                Status = "ok",
                Response = new HyperliquidExchangeResponseData
                {
                    Type = "order",
                    Data = new HyperliquidOrderResponseData
                    {
                        Statuses =
                        [
                            new HyperliquidOrderStatus { Resting = new HyperliquidRestingOrder { Oid = 12345 } },
                        ],
                    },
                },
            });

        var result = await _sut.PlaceOrderAsync(request);

        result.Success.Should().BeTrue();
        result.OrderId.Should().Be("12345");
        result.Status.Should().Be("open");
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenExchangeStatusError_WhenPlaceOrderAsync_ThenReturnsRejectedWithDetail()
    {
        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP",
            Side = "buy",
            OrderType = "limit",
            Price = 65000m,
            Size = 0.001m,
        };

        _restClientMock
            .Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HyperliquidExchangeResponse
            {
                Status = "ok",
                Response = new HyperliquidExchangeResponseData
                {
                    Type = "order",
                    Data = new HyperliquidOrderResponseData
                    {
                        Statuses = [new HyperliquidOrderStatus { Error = "Insufficient margin" }],
                    },
                },
            });

        var result = await _sut.PlaceOrderAsync(request);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("rejected");
        result.Detail.Should().Be("Insufficient margin");
    }

    [TestMethod]
    public async Task GivenSignatureRejection_WhenPlaceOrderAsync_ThenReturnsSignatureRejected()
    {
        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP",
            Side = "buy",
            OrderType = "limit",
            Price = 65000m,
            Size = 0.001m,
        };

        _restClientMock
            .Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("signature rejected by exchange"));

        var result = await _sut.PlaceOrderAsync(request);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("signature_rejected");
        result.Detail.Should().Contain("signature");
    }

    [TestMethod]
    public async Task GivenLimitOrderWithoutPrice_WhenPlaceOrderAsync_ThenThrowsDomainException()
    {
        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP",
            Side = "buy",
            OrderType = "limit",
            Price = null,
            Size = 0.001m,
        };

        var action = () => _sut.PlaceOrderAsync(request);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("*Price*required*limit*");
    }

    [TestMethod]
    public async Task GivenTestSign_WhenTestSignAsync_ThenReturnsSignatureWithoutExchangeSubmission()
    {
        var result = await _sut.TestSignAsync();

        result.DomainSeparator.Should().StartWith("0x");
        result.TypeHash.Should().StartWith("0x");
        result.MessageHash.Should().StartWith("0x");
        result.Signature.V.Should().Be(27);
        result.Signature.R.Should().StartWith("0x");
        result.Signature.S.Should().StartWith("0x");

        _restClientMock.Verify(
            r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
