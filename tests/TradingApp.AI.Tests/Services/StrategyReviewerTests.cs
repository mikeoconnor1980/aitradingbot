using Microsoft.Extensions.Logging;
using TradingApp.AI.Services;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.AI.Tests.Services;

[TestClass]
public sealed class StrategyReviewerTests
{
    private Mock<IReviewLlmClient> _llmClientMock = default!;
    private StrategyReviewer _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _llmClientMock = new Mock<IReviewLlmClient>();
        _sut = new StrategyReviewer(
            _llmClientMock.Object,
            Mock.Of<ILogger<StrategyReviewer>>());
    }

    [TestMethod]
    public async Task GivenValidStrategyJson_WhenReviewAsync_ThenReturnsReviewMarkdown()
    {
        const string strategyJson = "{\"grid\":{}}";
        const string expectedReview = "## 1. Strategy Summary\n- Grid strategy";

        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReview);

        var result = await _sut.ReviewAsync(strategyJson, CancellationToken.None);

        result.Should().Be(expectedReview);
        _llmClientMock.Verify(
            client => client.CompleteAsync(
                It.Is<string>(prompt => prompt.Contains("You are an expert trading strategy reviewer.", StringComparison.Ordinal)),
                It.Is<string>(message =>
                    message.Contains("Return only the final end-user markdown review.", StringComparison.Ordinal) &&
                    message.Contains("```json", StringComparison.Ordinal) &&
                    message.Contains(strategyJson, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenMarkdownCodeFence_WhenReviewAsync_ThenStripsFenceBeforeReturning()
    {
        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("```markdown\n## 1. Strategy Summary\n- Clean output\n```");

        var result = await _sut.ReviewAsync("{\"grid\":{}}", CancellationToken.None);

        result.Should().Be("## 1. Strategy Summary\n- Clean output");
    }

    [TestMethod]
    public async Task GivenRawJsonResponse_WhenReviewAsync_ThenReturnsFormattedFallbackReview()
    {
        const string strategyJson = """
            {
              "strategyName": "ETH RSI Buy",
              "strategyMode": "signal",
              "market": "ETH",
              "timeframe": "15m",
              "direction": "long",
              "entryConditions": [ { "type": "rsi" } ],
              "exit": {
                "takeProfit": { "enabled": true, "value": 3 },
                "stopLoss": { "enabled": true, "value": 5 }
              },
              "risk": {
                "leverage": 1,
                "maxOpenTrades": 1
              }
            }
            """;

        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategyJson);

        var result = await _sut.ReviewAsync(strategyJson, CancellationToken.None);

        result.Should().Contain("## 1. Strategy Summary");
        result.Should().Contain("ETH RSI Buy");
        result.Should().Contain("## 3. Exit Logic Completeness");
        result.Should().NotStartWith("{");
    }

    [TestMethod]
    public async Task GivenLlmFailure_WhenReviewAsync_ThenThrowsHttpRequestException()
    {
        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM unavailable"));

        var act = () => _sut.ReviewAsync("{\"grid\":{}}", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task GivenInvalidInput_WhenReviewAsync_ThenThrowsArgumentException(string? strategyJson)
    {
        var act = () => _sut.ReviewAsync(strategyJson!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}