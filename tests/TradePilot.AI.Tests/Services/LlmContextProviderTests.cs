using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TradePilot.AI.Services;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MacroCalendar.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.AI.Tests.Services;

[TestClass]
public sealed class LlmContextProviderTests
{
    private Mock<ILlmContextClient> _llmClientMock = default!;
    private LlmContextProvider _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _llmClientMock = new Mock<ILlmContextClient>();

        var options = Options.Create(new LlmContextOptions
        {
            CacheDurationSeconds = 900,
        });

        _sut = new LlmContextProvider(
            _llmClientMock.Object,
            options,
            Mock.Of<ILogger<LlmContextProvider>>());
    }

    [TestMethod]
    public async Task GivenValidLlmResponse_WhenGetContextAsync_ThenReturnsPopulatedLlmContext()
    {
        const string llmResponse = """
            {
              "marketSentiment": "Bearish",
              "macroRegime": "Bearish",
              "eventRisk": "High",
              "confidence": 0.82,
              "derivedRegime": "RiskOff",
              "summary": "Strong bearish momentum with elevated volatility."
            }
            """;

        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        var result = await _sut.GetContextAsync("BTC-USD", CreateIndicators(), cancellationToken: CancellationToken.None);

        result.Should().NotBeNull();
        result!.MarketSentiment.Should().Be("Bearish");
        result.MacroRegime.Should().Be("Bearish");
        result.EventRisk.Should().Be("High");
        result.Confidence.Should().Be(0.82m);
        result.DerivedRegime.Should().Be(MarketRegime.RiskOff);
        result.Summary.Should().Contain("bearish");
    }

    [TestMethod]
    public async Task GivenBullishResponse_WhenGetContextAsync_ThenMapsAggressiveRegime()
    {
        const string llmResponse = """
            {
              "marketSentiment": "Bullish",
              "macroRegime": "Bullish",
              "eventRisk": "Low",
              "confidence": 0.90,
              "derivedRegime": "Aggressive",
              "summary": "Strong uptrend with low volatility."
            }
            """;

        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        var result = await _sut.GetContextAsync("BTC-USD", CreateIndicators(), cancellationToken: CancellationToken.None);

        result.Should().NotBeNull();
        result!.DerivedRegime.Should().Be(MarketRegime.Aggressive);
        result.MarketSentiment.Should().Be("Bullish");
    }

    [TestMethod]
    public async Task GivenCachedResult_WhenCalledAgain_ThenReturnsCachedWithoutCallingLlm()
    {
        const string llmResponse = """
            {
              "marketSentiment": "Neutral",
              "macroRegime": "Neutral",
              "eventRisk": "Low",
              "confidence": 0.60,
              "derivedRegime": "Normal",
              "summary": "Range-bound."
            }
            """;

        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        var first = await _sut.GetContextAsync("BTC-USD", CreateIndicators(), cancellationToken: CancellationToken.None);
        var second = await _sut.GetContextAsync("BTC-USD", CreateIndicators(), cancellationToken: CancellationToken.None);

        first.Should().NotBeNull();
        second.Should().BeSameAs(first);
        _llmClientMock.Verify(
            c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenLlmFailure_WhenGetContextAsync_ThenReturnsNullGracefully()
    {
        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM unavailable"));

        var result = await _sut.GetContextAsync("BTC-USD", CreateIndicators(), cancellationToken: CancellationToken.None);

        result.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenLlmFailureWithCachedResult_WhenGetContextAsync_ThenReturnsCached()
    {
        const string llmResponse = """
            {
              "marketSentiment": "Bullish",
              "macroRegime": "Bullish",
              "eventRisk": "Low",
              "confidence": 0.85,
              "derivedRegime": "Aggressive",
              "summary": "Strong uptrend."
            }
            """;

        var clientMock = new Mock<ILlmContextClient>();
        clientMock
            .SetupSequence(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse)
            .ThrowsAsync(new HttpRequestException("LLM down"));

        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cacheOptions = Options.Create(new LlmContextOptions { CacheDurationSeconds = 900 });
        var sut = new LlmContextProvider(
            clientMock.Object,
            cacheOptions,
            Mock.Of<ILogger<LlmContextProvider>>(),
            fakeTime);

        // First call succeeds - populates cache
        var first = await sut.GetContextAsync("BTC-USD", CreateIndicators(), cancellationToken: CancellationToken.None);
        first.Should().NotBeNull();
        first!.DerivedRegime.Should().Be(MarketRegime.Aggressive);

        // Advance time past cache expiry
        fakeTime.Advance(TimeSpan.FromSeconds(901));

        // Second call fails - should return stale cached result
        var second = await sut.GetContextAsync("BTC-USD", CreateIndicators(), cancellationToken: CancellationToken.None);
        second.Should().NotBeNull();
        second!.DerivedRegime.Should().Be(MarketRegime.Aggressive);
    }

    [TestMethod]
    public async Task GivenCodeFencedResponse_WhenGetContextAsync_ThenStripsAndParses()
    {
        const string llmResponse = """
            ```json
            {
              "marketSentiment": "Bullish",
              "macroRegime": "Bullish",
              "eventRisk": "Low",
              "confidence": 0.75,
              "derivedRegime": "Normal",
              "summary": "Neutral conditions."
            }
            ```
            """;

        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        var result = await _sut.GetContextAsync("BTC-USD", CreateIndicators(), cancellationToken: CancellationToken.None);

        result.Should().NotBeNull();
        result!.DerivedRegime.Should().Be(MarketRegime.Normal);
    }

    [TestMethod]
    public async Task GivenGarbageJsonResponse_WhenGetContextAsync_ThenReturnsFallbackContext()
    {
        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not valid json at all");

        var result = await _sut.GetContextAsync("BTC-USD", CreateIndicators(), cancellationToken: CancellationToken.None);

        result.Should().NotBeNull();
        result!.DerivedRegime.Should().Be(MarketRegime.Normal);
        result.Confidence.Should().Be(0m);
        result.Summary.Should().Contain("Failed to parse");
    }

    [TestMethod]
    public async Task GivenUnknownRegimeValue_WhenGetContextAsync_ThenDefaultsToNormal()
    {
        const string llmResponse = """
            {
              "marketSentiment": "MildlyBullish",
              "macroRegime": "Uncertain",
              "eventRisk": "Moderate",
              "confidence": 0.50,
              "derivedRegime": "CautiouslyOptimistic",
              "summary": "Mixed signals."
            }
            """;

        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        var result = await _sut.GetContextAsync("BTC-USD", CreateIndicators(), cancellationToken: CancellationToken.None);

        result.Should().NotBeNull();
        result!.DerivedRegime.Should().Be(MarketRegime.Normal);
        result.MarketSentiment.Should().Be("Neutral"); // unknown → Neutral
        result.EventRisk.Should().Be("Low"); // unknown → Low
    }

    [TestMethod]
    public async Task GivenConfidenceOutOfRange_WhenGetContextAsync_ThenClampedTo0To1()
    {
        const string llmResponse = """
            {
              "marketSentiment": "Neutral",
              "macroRegime": "Neutral",
              "eventRisk": "Low",
              "confidence": 1.5,
              "derivedRegime": "Normal",
              "summary": "Over-confident."
            }
            """;

        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        var result = await _sut.GetContextAsync("BTC-USD", CreateIndicators(), cancellationToken: CancellationToken.None);

        result.Should().NotBeNull();
        result!.Confidence.Should().Be(1.0m);
    }

    [TestMethod]
    public void GivenIndicators_WhenBuildUserMessage_ThenContainsAllValues()
    {
        var indicators = new IndicatorSnapshot
        {
            EmaFast = 50000.50m,
            EmaSlow = 49500.25m,
            EmaTrend = 48000.00m,
            Rsi = 62.5m,
            Atr = 450.1234m,
        };

        var message = LlmContextProvider.BuildUserMessage("BTC-USD", indicators);

        message.Should().Contain("BTC-USD");
        message.Should().Contain("50000.50");
        message.Should().Contain("49500.25");
        message.Should().Contain("48000.00");
        message.Should().Contain("62.50");
        message.Should().Contain("450.1234");
        message.Should().Contain("Bullish stack");
    }

    [TestMethod]
    public void GivenBearishStack_WhenBuildUserMessage_ThenDescribesAsBearish()
    {
        var indicators = new IndicatorSnapshot
        {
            EmaFast = 45000m,
            EmaSlow = 47000m,
            EmaTrend = 50000m,
            Rsi = 35m,
            Atr = 300m,
        };

        var message = LlmContextProvider.BuildUserMessage("ETH-USD", indicators);

        message.Should().Contain("Bearish stack");
    }

    [TestMethod]
    public void GivenValidJson_WhenParseResponse_ThenReturnsCorrectLlmContext()
    {
        const string json = """
            {
              "marketSentiment": "Bearish",
              "macroRegime": "Bearish",
              "eventRisk": "High",
              "confidence": 0.8,
              "derivedRegime": "Defensive",
              "summary": "Bearish with high vol."
            }
            """;

        var result = LlmContextProvider.ParseResponse(json, "BTC-USD");

        result.DerivedRegime.Should().Be(MarketRegime.Defensive);
        result.MarketSentiment.Should().Be("Bearish");
        result.MacroRegime.Should().Be("Bearish");
        result.EventRisk.Should().Be("High");
        result.Confidence.Should().Be(0.8m);
        result.Summary.Should().Be("Bearish with high vol.");
    }

    [TestMethod]
    public void GivenInvalidJson_WhenParseResponse_ThenReturnsFallback()
    {
        var result = LlmContextProvider.ParseResponse("not json", "BTC-USD");

        result.DerivedRegime.Should().Be(MarketRegime.Normal);
        result.Confidence.Should().Be(0m);
    }

    [TestMethod]
    public async Task GivenOperationCanceled_WhenGetContextAsync_ThenPropagatesCancellation()
    {
        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _sut.GetContextAsync("BTC-USD", CreateIndicators(), cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static IndicatorSnapshot CreateIndicators()
    {
        return new IndicatorSnapshot
        {
            EmaFast = 50000m,
            EmaSlow = 49000m,
            EmaTrend = 48000m,
            Rsi = 55m,
            Atr = 400m,
        };
    }

    [TestMethod]
    public void GivenUpcomingMacroEvents_WhenBuildUserMessage_ThenIncludesEventDetails()
    {
        var events = new List<MacroEventListItemDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Non-Farm Payroll",
                Country = "US",
                Currency = "USD",
                Category = "Employment",
                ScheduledAtUtc = DateTimeOffset.Parse("2026-04-07T13:30:00Z").ToUnixTimeMilliseconds(),
                Importance = MacroEventImportance.High,
                Status = MacroEventStatus.Scheduled,
                Forecast = "180K",
                Previous = "151K",
                BlockStartUtc = 0,
                BlockEndUtc = 0,
                IsBlockingNow = false
            }
        };

        var message = LlmContextProvider.BuildUserMessage("BTC-USD", CreateIndicators(), events);

        message.Should().Contain("Upcoming macro events");
        message.Should().Contain("Non-Farm Payroll");
        message.Should().Contain("[High]");
        message.Should().Contain("US/USD");
        message.Should().Contain("Employment");
        message.Should().Contain("Forecast: 180K");
        message.Should().Contain("Previous: 151K");
    }

    [TestMethod]
    public void GivenBlockingMacroEvent_WhenBuildUserMessage_ThenIncludesBlockWarning()
    {
        var events = new List<MacroEventListItemDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "FOMC Rate Decision",
                Country = "US",
                Currency = "USD",
                Category = "Interest Rate",
                ScheduledAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Importance = MacroEventImportance.Critical,
                Status = MacroEventStatus.Scheduled,
                BlockStartUtc = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeMilliseconds(),
                BlockEndUtc = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeMilliseconds(),
                IsBlockingNow = true
            }
        };

        var message = LlmContextProvider.BuildUserMessage("BTC-USD", CreateIndicators(), events);

        message.Should().Contain("Active macro event block windows");
        message.Should().Contain("FOMC Rate Decision");
        message.Should().Contain("[Critical]");
    }

    [TestMethod]
    public void GivenNoMacroEvents_WhenBuildUserMessage_ThenIncludesNoEventsMessage()
    {
        var message = LlmContextProvider.BuildUserMessage("BTC-USD", CreateIndicators());

        message.Should().Contain("No upcoming macro events");
    }

    [TestMethod]
    public void GivenEmptyMacroEventsList_WhenBuildUserMessage_ThenIncludesNoEventsMessage()
    {
        var message = LlmContextProvider.BuildUserMessage("BTC-USD", CreateIndicators(), []);

        message.Should().Contain("No upcoming macro events");
    }

    [TestMethod]
    public async Task GivenMacroEvents_WhenGetContextAsync_ThenPassesEventsToLlm()
    {
        const string llmResponse = """
            {
              "marketSentiment": "Neutral",
              "macroRegime": "Neutral",
              "eventRisk": "High",
              "confidence": 0.85,
              "derivedRegime": "RiskOff",
              "summary": "High event risk due to imminent FOMC decision."
            }
            """;

        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        var events = new List<MacroEventListItemDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "FOMC",
                Country = "US",
                Currency = "USD",
                Category = "Interest Rate",
                ScheduledAtUtc = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds(),
                Importance = MacroEventImportance.Critical,
                Status = MacroEventStatus.Scheduled,
                BlockStartUtc = 0,
                BlockEndUtc = 0,
                IsBlockingNow = false
            }
        };

        var result = await _sut.GetContextAsync("BTC-USD", CreateIndicators(), events, fearGreed: null, CancellationToken.None);

        result.Should().NotBeNull();
        result!.EventRisk.Should().Be("High");
        result.DerivedRegime.Should().Be(MarketRegime.RiskOff);

        // Verify the LLM was called with a message containing the event
        _llmClientMock.Verify(
            c => c.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(msg => msg.Contains("FOMC")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
