using Moq;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.LlmContextSnapshots.Queries;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.LlmContextSnapshots.Queries;

[TestClass]
public sealed class GetContextHistoryQueryHandlerTests
{
    private readonly Mock<ILlmContextSnapshotRepository> _repositoryMock = new();

    [TestMethod]
    public async Task GivenSnapshots_WhenHandle_ThenReturnsMappedList()
    {
        var snapshots = new List<LlmContextSnapshot>
        {
            LlmContextSnapshot.Create("BTC-USD", "Neutral", "Neutral", "Low", 0.70m, "Normal", "S1", 1712000000000),
            LlmContextSnapshot.Create("BTC-USD", "Bullish", "Neutral", "Low", 0.85m, "Aggressive", "S2", 1712003600000),
        };

        _repositoryMock
            .Setup(r => r.GetHistoryAsync("BTC-USD", 1712000000000, 1712003600000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        var sut = new GetContextHistoryQueryHandler(_repositoryMock.Object);
        var result = await sut.Handle(
            new GetContextHistoryQuery("BTC-USD", 1712000000000, 1712003600000),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].DerivedRegime.Should().Be("Normal");
        result[1].DerivedRegime.Should().Be("Aggressive");
    }

    [TestMethod]
    public async Task GivenNoSnapshots_WhenHandle_ThenReturnsEmptyList()
    {
        _repositoryMock
            .Setup(r => r.GetHistoryAsync("BTC-USD", 0, long.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LlmContextSnapshot>());

        var sut = new GetContextHistoryQueryHandler(_repositoryMock.Object);
        var result = await sut.Handle(
            new GetContextHistoryQuery("BTC-USD", 0, long.MaxValue),
            CancellationToken.None);

        result.Should().BeEmpty();
    }
}
