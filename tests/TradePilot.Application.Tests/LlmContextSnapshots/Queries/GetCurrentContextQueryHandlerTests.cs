using Moq;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.LlmContextSnapshots.Queries;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.LlmContextSnapshots.Queries;

[TestClass]
public sealed class GetCurrentContextQueryHandlerTests
{
    private readonly Mock<ILlmContextSnapshotRepository> _repositoryMock = new();

    [TestMethod]
    public async Task GivenSnapshot_WhenHandle_ThenReturnsMappedDto()
    {
        var snapshot = LlmContextSnapshot.Create(
            "BTC-USD", "Bullish", "Neutral", "Low", 0.85m, "Normal", "All clear.", 1712000000000);

        _repositoryMock
            .Setup(r => r.GetLatestAsync("BTC-USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var sut = new GetCurrentContextQueryHandler(_repositoryMock.Object);
        var result = await sut.Handle(new GetCurrentContextQuery("BTC-USD"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Symbol.Should().Be("BTC-USD");
        result.MarketSentiment.Should().Be("Bullish");
        result.MacroRegime.Should().Be("Neutral");
        result.EventRisk.Should().Be("Low");
        result.Confidence.Should().Be(0.85m);
        result.DerivedRegime.Should().Be("Normal");
        result.Summary.Should().Be("All clear.");
        result.GeneratedAtUtc.Should().Be(1712000000000);
    }

    [TestMethod]
    public async Task GivenNoSnapshot_WhenHandle_ThenReturnsNull()
    {
        _repositoryMock
            .Setup(r => r.GetLatestAsync("BTC-USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmContextSnapshot?)null);

        var sut = new GetCurrentContextQueryHandler(_repositoryMock.Object);
        var result = await sut.Handle(new GetCurrentContextQuery("BTC-USD"), CancellationToken.None);

        result.Should().BeNull();
    }
}
