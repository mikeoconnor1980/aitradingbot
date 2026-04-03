using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Commands;
using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.Tests.StrategyAuthoring.Commands;

[TestClass]
public sealed class InterpretStrategyCommandHandlerTests
{
    [TestMethod]
    public async Task GivenCommand_WhenHandle_ThenDelegatesToInterpreter()
    {
        var interpreterMock = new Mock<IStrategyInterpreter>();
        var expected = new StrategyIntentDto
        {
            Config = new StrategyConfig { StrategyName = "Interpreted Strategy" },
            Confidence = 0.7m,
        };

        interpreterMock
            .Setup(interpreter => interpreter.InterpretAsync("trade BTC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new InterpretStrategyCommandHandler(interpreterMock.Object);

        var result = await sut.Handle(new InterpretStrategyCommand("trade BTC"), CancellationToken.None);

        result.Should().BeSameAs(expected);
        interpreterMock.Verify(
            interpreter => interpreter.InterpretAsync("trade BTC", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}