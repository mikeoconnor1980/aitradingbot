using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradePilot.AI.Analyst;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Analyst.Models;

namespace TradePilot.AI.Tests.Analyst;

[TestClass]
public sealed class TradingAnalystTests
{
    private static readonly IReadOnlyList<AnalystToolDefinition> Definitions =
    [
        Definition("analyse_market_multi_timeframe"),
        Definition("get_account_summary"),
        Definition("get_positions"),
        Definition("get_latest_strategy_evaluation"),
    ];

    [TestMethod]
    public async Task GivenMarketQuestion_WhenLlmRequestsAnalysis_ThenStructuredResultIsReturnedBeforeFinalResponse()
    {
        var llm = new FakeAnalystLlmClient(
            ToolResponse(Call("call-1", "analyse_market_multi_timeframe", "{\"symbol\":\"BTC\"}")),
            FinalResponse("BTC is bullish on the primary trend."));
        var catalog = CreateCatalog();
        catalog.Setup(service => service.ExecuteAsync(
                "analyse_market_multi_timeframe",
                "{\"symbol\":\"BTC\"}",
                It.IsAny<AnalystToolContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success(new { primaryTrend = "bullish", shortTermTrend = "bearish", trendConflict = true }));
        var sut = CreateSut(llm, catalog.Object);

        var result = await sut.AnalyseAsync(
            new TradingAnalystRequest("What is happening with BTC?"),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Response.Should().Be("BTC is bullish on the primary trend.");
        result.ToolInvocations.Should().ContainSingle(invocation =>
            invocation.ToolName == "analyse_market_multi_timeframe" && invocation.Succeeded);
        result.ToolInvocations[0].Result!.Value.GetProperty("primaryTrend").GetString().Should().Be("bullish");
        llm.Requests.Should().HaveCount(2);
        llm.Requests[1].Messages.Should().Contain(message =>
            message.Role == "tool" && message.Content!.Contains("primaryTrend", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GivenPositionAwareQuestion_WhenLlmRequestsPositionAndMarket_ThenBothExecuteInOrder()
    {
        var llm = new FakeAnalystLlmClient(
            ToolResponse(
                Call("call-1", "get_positions", "{}"),
                Call("call-2", "analyse_market_multi_timeframe", "{\"symbol\":\"BTC\"}")),
            FinalResponse("Your long is aligned with the primary trend but conflicts short term."));
        var order = new List<string>();
        var catalog = CreateCatalog();
        catalog.Setup(service => service.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AnalystToolContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, AnalystToolContext, CancellationToken>((name, _, _, _) => order.Add(name))
            .ReturnsAsync((string name, string arguments, AnalystToolContext context, CancellationToken cancellationToken) =>
                name == "get_positions"
                    ? Success(new[] { new { asset = "BTC", side = "Long" } })
                    : Success(new { primaryTrend = "bullish", shortTermTrend = "bearish", trendConflict = true }));
        var sut = CreateSut(llm, catalog.Object);

        var result = await sut.AnalyseAsync(
            new TradingAnalystRequest("How does BTC currently look relative to my position?", Guid.NewGuid()),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        order.Should().Equal("get_positions", "analyse_market_multi_timeframe");
        result.ToolInvocations.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task GivenWhyStrategyDidNotTrade_WhenLlmRequestsEvidence_ThenLatestEvaluationIsUsedInsteadOfMarketAnalysis()
    {
        var llm = new FakeAnalystLlmClient(
            ToolResponse(Call(
                "evaluation",
                "get_latest_strategy_evaluation",
                "{\"strategyName\":\"v10.4\",\"symbol\":\"BTC\"}")),
            FinalResponse("The recorded RSI rule blocked the setup."));
        var catalog = CreateCatalog();
        catalog.Setup(service => service.ExecuteAsync(
                "get_latest_strategy_evaluation",
                It.IsAny<string>(),
                It.IsAny<AnalystToolContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success(new
            {
                decision = "no_trade",
                primaryRejectionReason = "RSI 67.3 exceeded 62.",
                rules = new[] { new { ruleId = "entry.rsi.max", passed = false } },
            }));
        var sut = CreateSut(llm, catalog.Object);

        var result = await sut.AnalyseAsync(
            new TradingAnalystRequest("Why didn't v10.4 trade BTC?"),
            CancellationToken.None);

        result.Response.Should().Be("The recorded RSI rule blocked the setup.");
        result.ToolInvocations.Should().ContainSingle(invocation =>
            invocation.ToolName == "get_latest_strategy_evaluation" && invocation.Succeeded);
        catalog.Verify(service => service.ExecuteAsync(
            "analyse_market_multi_timeframe",
            It.IsAny<string>(),
            It.IsAny<AnalystToolContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
        llm.Requests[1].Messages.Should().Contain(message =>
            message.Role == "tool" && message.Content!.Contains("entry.rsi.max", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GivenWhyStrategyDidNotTradeWithoutEvidence_WhenLlmContinues_ThenMissingEvidenceIsReturnedForHonestAnswer()
    {
        var llm = new FakeAnalystLlmClient(
            ToolResponse(Call(
                "evaluation",
                "get_latest_strategy_evaluation",
                "{\"strategyName\":\"v10.4\",\"symbol\":\"BTC\"}")),
            FinalResponse("No recorded strategy evaluation was available for that period."));
        var catalog = CreateCatalog();
        catalog.Setup(service => service.ExecuteAsync(
                "get_latest_strategy_evaluation",
                It.IsAny<string>(),
                It.IsAny<AnalystToolContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalystToolResult.Failure(
                "no_evaluation_evidence",
                "No recorded strategy evaluation was available for the requested strategy and period."));
        var sut = CreateSut(llm, catalog.Object);

        var result = await sut.AnalyseAsync(
            new TradingAnalystRequest("Why didn't v10.4 trade BTC last Tuesday?"),
            CancellationToken.None);

        result.Response.Should().Be("No recorded strategy evaluation was available for that period.");
        result.ToolInvocations.Should().ContainSingle(invocation =>
            invocation.ErrorCode == "no_evaluation_evidence");
    }

    [TestMethod]
    public async Task GivenThreeOpenPositions_WhenLlmRequestsEachMarket_ThenAllCallsExecuteInOneRequest()
    {
        var llm = new FakeAnalystLlmClient(
            ToolResponse(Call("positions", "get_positions", "{}")),
            ToolResponse(
                Call("btc", "analyse_market_multi_timeframe", "{\"symbol\":\"BTC\"}"),
                Call("eth", "analyse_market_multi_timeframe", "{\"symbol\":\"ETH\"}"),
                Call("sol", "analyse_market_multi_timeframe", "{\"symbol\":\"SOL\"}")),
            FinalResponse("BTC, ETH, and SOL were analysed."));
        var symbols = new List<string>();
        var catalog = CreateCatalog();
        catalog.Setup(service => service.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AnalystToolContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, AnalystToolContext, CancellationToken>((name, arguments, _, _) =>
            {
                if (name == "analyse_market_multi_timeframe")
                {
                    symbols.Add(JsonDocument.Parse(arguments).RootElement.GetProperty("symbol").GetString()!);
                }
            })
            .ReturnsAsync(Success(new { fact = "structured" }));
        var sut = CreateSut(llm, catalog.Object);

        var result = await sut.AnalyseAsync(
            new TradingAnalystRequest("Analyse all my open positions.", Guid.NewGuid()),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        symbols.Should().Equal("BTC", "ETH", "SOL");
        result.ToolInvocations.Should().HaveCount(4);
    }

    [DataTestMethod]
    [DataRow("unknown_tool")]
    [DataRow("place_order")]
    public async Task GivenUnavailableTool_WhenRequested_ThenCatalogueIsNotInvokedAndSafeErrorReturnsToModel(string toolName)
    {
        var llm = new FakeAnalystLlmClient(
            ToolResponse(Call("bad-call", toolName, "{}")),
            FinalResponse("That capability is unavailable."));
        var catalog = CreateCatalog();
        var sut = CreateSut(llm, catalog.Object);

        var result = await sut.AnalyseAsync(
            new TradingAnalystRequest("Do the unavailable thing."),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.ToolInvocations.Should().ContainSingle(invocation =>
            invocation.ToolName == toolName && invocation.ErrorCode == "unknown_tool");
        catalog.Verify(service => service.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<AnalystToolContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
        llm.Requests[1].Messages.Should().Contain(message =>
            message.Role == "tool" && message.Content!.Contains("unknown_tool", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GivenInvalidArgumentsOrApplicationFailure_WhenToolReturnsError_ThenNoFactIsFabricated()
    {
        var llm = new FakeAnalystLlmClient(
            ToolResponse(Call("call-1", "analyse_market_multi_timeframe", "{bad-json")),
            FinalResponse("I couldn't retrieve the current market state."));
        var catalog = CreateCatalog();
        catalog.Setup(service => service.ExecuteAsync(
                "analyse_market_multi_timeframe",
                "{bad-json",
                It.IsAny<AnalystToolContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalystToolResult.Failure("invalid_arguments", "The tool arguments were invalid."));
        var sut = CreateSut(llm, catalog.Object);

        var result = await sut.AnalyseAsync(
            new TradingAnalystRequest("What is BTC doing?"),
            CancellationToken.None);

        result.Response.Should().Be("I couldn't retrieve the current market state.");
        result.ToolInvocations[0].Arguments.Should().Be("<invalid-json>");
        result.ToolInvocations[0].ErrorCode.Should().Be("invalid_arguments");
        llm.Requests[1].Messages.Should().Contain(message =>
            message.Role == "tool" && message.Content!.Contains("invalid_arguments", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GivenApplicationFailure_WhenModelContinues_ThenSafeFailureIsAvailableForHonestFinalResponse()
    {
        var llm = new FakeAnalystLlmClient(
            ToolResponse(Call("call-1", "get_positions", "{}")),
            FinalResponse("I couldn't retrieve the current account state."));
        var catalog = CreateCatalog();
        catalog.Setup(service => service.ExecuteAsync(
                "get_positions",
                "{}",
                It.IsAny<AnalystToolContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalystToolResult.Failure("data_unavailable", "Current TradePilot data is temporarily unavailable."));
        var sut = CreateSut(llm, catalog.Object);

        var result = await sut.AnalyseAsync(
            new TradingAnalystRequest("What should I pay attention to?", Guid.NewGuid()),
            CancellationToken.None);

        result.Response.Should().Be("I couldn't retrieve the current account state.");
        result.ToolInvocations.Should().ContainSingle(invocation =>
            !invocation.Succeeded && invocation.ErrorCode == "data_unavailable");
        llm.Requests[1].Messages.Should().Contain(message =>
            message.Role == "tool" && message.Content!.Contains("data_unavailable", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GivenRepeatedExactToolCall_WhenAnalysed_ThenRequestScopedResultIsReused()
    {
        var repeatedCall = Call("call-1", "analyse_market_multi_timeframe", "{\"symbol\":\"BTC\"}");
        var llm = new FakeAnalystLlmClient(
            ToolResponse(repeatedCall),
            ToolResponse(repeatedCall with { Id = "call-2" }),
            FinalResponse("The cached TradePilot facts are unchanged."));
        var catalog = CreateCatalog();
        catalog.Setup(service => service.ExecuteAsync(
                "analyse_market_multi_timeframe",
                It.IsAny<string>(),
                It.IsAny<AnalystToolContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success(new { primaryTrend = "bullish" }));
        var sut = CreateSut(llm, catalog.Object);

        var result = await sut.AnalyseAsync(
            new TradingAnalystRequest("What is BTC doing?"),
            CancellationToken.None);

        catalog.Verify(service => service.ExecuteAsync(
            "analyse_market_multi_timeframe",
            It.IsAny<string>(),
            It.IsAny<AnalystToolContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
        result.ToolInvocations.Should().HaveCount(2);
        result.ToolInvocations[1].WasCached.Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenRepeatedToolRounds_WhenLimitReached_ThenForcedFinalRoundHasNoTools()
    {
        var llm = new FakeAnalystLlmClient(
            ToolResponse(Call("call-1", "analyse_market_multi_timeframe", "{\"symbol\":\"BTC\"}")),
            ToolResponse(Call("call-2", "analyse_market_multi_timeframe", "{\"symbol\":\"ETH\"}")),
            FinalResponse("I reached the tool limit and can only summarise the gathered facts."));
        var catalog = CreateCatalog();
        catalog.Setup(service => service.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AnalystToolContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success(new { fact = "structured" }));
        var sut = CreateSut(llm, catalog.Object, maxToolRounds: 2);

        var result = await sut.AnalyseAsync(
            new TradingAnalystRequest("Keep calling tools."),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.ToolRounds.Should().Be(2);
        llm.Requests.Should().HaveCount(3);
        llm.Requests[2].Tools.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenToolCallStorm_WhenCallLimitWouldBeExceeded_ThenNoCallsInThatBatchExecute()
    {
        var llm = new FakeAnalystLlmClient(
            ToolResponse(
                Call("call-1", "analyse_market_multi_timeframe", "{\"symbol\":\"BTC\"}"),
                Call("call-2", "analyse_market_multi_timeframe", "{\"symbol\":\"ETH\"}")),
            FinalResponse("No tools ran because the request exceeded the call limit."));
        var catalog = CreateCatalog();
        var sut = CreateSut(llm, catalog.Object, maxToolCalls: 1);

        var result = await sut.AnalyseAsync(
            new TradingAnalystRequest("Create a tool storm."),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.ToolInvocations.Should().HaveCount(2)
            .And.OnlyContain(invocation => invocation.ErrorCode == "tool_limit_exceeded");
        catalog.Verify(service => service.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<AnalystToolContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
        llm.Requests[1].Tools.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenCancellation_WhenProviderIsCalled_ThenCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var llm = new Mock<IAnalystLlmClient>();
        llm.SetupGet(client => client.Provider).Returns("Fake");
        llm.SetupGet(client => client.Model).Returns("fake-model");
        llm.Setup(client => client.CompleteAsync(It.IsAny<AnalystLlmRequest>(), cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        var sut = CreateSut(llm.Object, CreateCatalog().Object);

        var action = () => sut.AnalyseAsync(
            new TradingAnalystRequest("What is BTC doing?"),
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        llm.Verify(client => client.CompleteAsync(It.IsAny<AnalystLlmRequest>(), cancellation.Token), Times.Once);
    }

    [TestMethod]
    public async Task GivenProviderFailure_WhenAnalysed_ThenControlledFailureIsReturned()
    {
        var llm = new Mock<IAnalystLlmClient>();
        llm.SetupGet(client => client.Provider).Returns("Fake");
        llm.SetupGet(client => client.Model).Returns("fake-model");
        llm.Setup(client => client.CompleteAsync(It.IsAny<AnalystLlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("secret provider detail"));
        var sut = CreateSut(llm.Object, CreateCatalog().Object);

        var result = await sut.AnalyseAsync(
            new TradingAnalystRequest("What is BTC doing?"),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.FailureCode.Should().Be("provider_failure");
        result.Response.Should().NotContain("secret");
    }

    private static TradingAnalyst CreateSut(
        IAnalystLlmClient llmClient,
        IAnalystToolCatalog catalog,
        int maxToolRounds = 5,
        int maxToolCalls = 10)
    {
        return new TradingAnalyst(
            llmClient,
            catalog,
            Options.Create(new LlmAnalystOptions { MaxToolRounds = maxToolRounds, MaxToolCalls = maxToolCalls }),
            NullLogger<TradingAnalyst>.Instance);
    }

    private static Mock<IAnalystToolCatalog> CreateCatalog()
    {
        var catalog = new Mock<IAnalystToolCatalog>();
        catalog.SetupGet(service => service.Definitions).Returns(Definitions);
        return catalog;
    }

    private static AnalystToolDefinition Definition(string name)
    {
        return new AnalystToolDefinition(
            name,
            $"Test definition for {name}.",
            JsonSerializer.SerializeToElement(new { type = "object" }));
    }

    private static AnalystLlmToolCall Call(string id, string name, string arguments)
    {
        return new AnalystLlmToolCall(id, name, arguments);
    }

    private static AnalystLlmResponse ToolResponse(params AnalystLlmToolCall[] calls)
    {
        return new AnalystLlmResponse(null, calls);
    }

    private static AnalystLlmResponse FinalResponse(string content)
    {
        return new AnalystLlmResponse(content, []);
    }

    private static AnalystToolResult Success<T>(T value)
    {
        return AnalystToolResult.Success(JsonSerializer.SerializeToElement(value));
    }

    private sealed class FakeAnalystLlmClient(params AnalystLlmResponse[] responses) : IAnalystLlmClient
    {
        private readonly Queue<AnalystLlmResponse> _responses = new(responses);

        public string Provider => "Fake";

        public string Model => "fake-model";

        public List<AnalystLlmRequest> Requests { get; } = [];

        public Task<AnalystLlmResponse> CompleteAsync(
            AnalystLlmRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
