using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.AI.Services;
using TradingApp.Application.Abstractions.Configuration;

namespace TradingApp.AI.Tests.Services;

[TestClass]
public sealed class OpenAiCompatibleLlmClientTests
{
    private Mock<ILogger<OpenAiCompatibleLlmClient>> _loggerMock = default!;
    private Mock<IOptions<LlmOptions>> _optionsMock = default!;

    [TestInitialize]
    public void Setup()
    {
        _optionsMock = new Mock<IOptions<LlmOptions>>();
        _optionsMock.Setup(options => options.Value).Returns(new LlmOptions
        {
            Provider = "Gemini",
            BaseUrl = "https://example.com/v1/",
            ModelName = "test-model",
            TimeoutSeconds = 30,
        });

        _loggerMock = new Mock<ILogger<OpenAiCompatibleLlmClient>>();
    }

    [TestMethod]
    public async Task GivenValidResponse_WhenCompleteAsync_ThenReturnsContentAndSendsExpectedPayload()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "test response" } } },
        });

        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });

        var sut = CreateSut(handler);

        var result = await sut.CompleteAsync("system prompt", "user prompt", CancellationToken.None);

        result.Should().Be("test response");
        handler.LastRequestUri.Should().Be("/v1/chat/completions");
        handler.LastRequestBody.Should().NotBeNull();

        using var payload = JsonDocument.Parse(handler.LastRequestBody!);
        payload.RootElement.GetProperty("model").GetString().Should().Be("test-model");
        payload.RootElement.GetProperty("temperature").GetDecimal().Should().Be(0.1m);
        payload.RootElement.GetProperty("response_format").GetProperty("type").GetString().Should().Be("json_object");
        payload.RootElement.GetProperty("messages")[0].GetProperty("role").GetString().Should().Be("system");
        payload.RootElement.GetProperty("messages")[0].GetProperty("content").GetString().Should().Be("system prompt");
        payload.RootElement.GetProperty("messages")[1].GetProperty("role").GetString().Should().Be("user");
        payload.RootElement.GetProperty("messages")[1].GetProperty("content").GetString().Should().Be("user prompt");
    }

    [TestMethod]
    public async Task GivenEmptyChoices_WhenCompleteAsync_ThenThrowsInvalidOperationException()
    {
        var responseJson = JsonSerializer.Serialize(new { choices = Array.Empty<object>() });
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });

        var sut = CreateSut(handler);

        var act = () => sut.CompleteAsync("system", "user", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no choices*");
    }

    [TestMethod]
    public async Task GivenServerError_WhenCompleteAsync_ThenThrowsHttpRequestException()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("error", Encoding.UTF8, "application/json"),
        });

        var sut = CreateSut(handler);

        var act = () => sut.CompleteAsync("system", "user", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*500*");
    }

    [TestMethod]
    public async Task GivenCanceledRequest_WhenCompleteAsync_ThenPropagatesOperationCanceledException()
    {
        var handler = new CapturingHttpMessageHandler(_ => throw new TaskCanceledException("timed out"));
        var sut = CreateSut(handler);

        var act = () => sut.CompleteAsync("system", "user", CancellationToken.None);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    private OpenAiCompatibleLlmClient CreateSut(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.com/v1/"),
        };

        return new OpenAiCompatibleLlmClient(httpClient, _optionsMock.Object, _loggerMock.Object);
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public string? LastRequestBody { get; private set; }

        public string? LastRequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.PathAndQuery;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return _responseFactory(request);
        }
    }
}