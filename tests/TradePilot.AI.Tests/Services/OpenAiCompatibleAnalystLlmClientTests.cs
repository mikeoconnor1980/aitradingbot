using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradePilot.AI.Services;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Analyst.Models;

namespace TradePilot.AI.Tests.Services;

[TestClass]
public sealed class OpenAiCompatibleAnalystLlmClientTests
{
    [TestMethod]
    public async Task GivenToolRequest_WhenCompleted_ThenCurrentChatCompletionsShapeIsMapped()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = (string?)null,
                        tool_calls = new[]
                        {
                            new
                            {
                                id = "call-1",
                                type = "function",
                                function = new { name = "get_positions", arguments = "{}" },
                            },
                        },
                    },
                },
            },
            usage = new { prompt_tokens = 10, completion_tokens = 4, total_tokens = 14 },
        });
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });
        var sut = CreateSut(handler);
        var definition = new AnalystToolDefinition(
            "get_positions",
            "Get positions.",
            JsonSerializer.SerializeToElement(new { type = "object" }));

        var result = await sut.CompleteAsync(
            new AnalystLlmRequest([new AnalystLlmMessage("user", "What are my positions?")], [definition]),
            CancellationToken.None);

        result.ToolCalls.Should().ContainSingle().Which.Should().Be(
            new AnalystLlmToolCall("call-1", "get_positions", "{}"));
        result.Usage.Should().Be(new AnalystTokenUsage(10, 4, 14));
        using var request = JsonDocument.Parse(handler.RequestBody!);
        request.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString()
            .Should().Be("get_positions");
        request.RootElement.GetProperty("tool_choice").GetString().Should().Be("auto");
    }

    [TestMethod]
    public async Task GivenToolResultMessagesAndNoAvailableTools_WhenCompleted_ThenProviderPayloadPreservesConversation()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { role = "assistant", content = "Final answer." } } },
        });
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });
        var sut = CreateSut(handler);
        var call = new AnalystLlmToolCall("call-1", "get_positions", "{}");

        var result = await sut.CompleteAsync(
            new AnalystLlmRequest(
            [
                new AnalystLlmMessage("assistant", ToolCalls: [call]),
                new AnalystLlmMessage("tool", "{\"succeeded\":true}", "call-1"),
            ],
            []),
            CancellationToken.None);

        result.Content.Should().Be("Final answer.");
        using var request = JsonDocument.Parse(handler.RequestBody!);
        request.RootElement.TryGetProperty("tools", out _).Should().BeFalse();
        request.RootElement.TryGetProperty("tool_choice", out _).Should().BeFalse();
        request.RootElement.GetProperty("messages")[0].GetProperty("tool_calls")[0]
            .GetProperty("function").GetProperty("arguments").GetString().Should().Be("{}");
        request.RootElement.GetProperty("messages")[1].GetProperty("tool_call_id").GetString().Should().Be("call-1");
    }

    [TestMethod]
    public async Task GivenMalformedToolCall_WhenCompleted_ThenControlledProviderErrorIsThrown()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        tool_calls = new[]
                        {
                            new { id = "", type = "function", function = new { name = "get_positions", arguments = "{}" } },
                        },
                    },
                },
            },
        });
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });
        var sut = CreateSut(handler);

        var action = () => sut.CompleteAsync(
            new AnalystLlmRequest([new AnalystLlmMessage("user", "Question")], [
                new AnalystToolDefinition(
                    "get_positions",
                    "Get positions.",
                    JsonSerializer.SerializeToElement(new { type = "object" }))]),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*malformed tool call*");
    }

    [TestMethod]
    public async Task GivenAzureResponsesFunctionCall_WhenCompleted_ThenMapsToolCallAndUsesResponsesEndpoint()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            output = new[]
            {
                new
                {
                    type = "function_call",
                    call_id = "call-azure-1",
                    name = "get_positions",
                    arguments = "{}",
                },
            },
            usage = new { input_tokens = 10, output_tokens = 4, total_tokens = 14 },
        });
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });
        var options = Options.Create(new LlmOptions
        {
            Provider = "AzureOpenAI",
            BaseUrl = "https://example.com/openai/responses?api-version=2025-04-01-preview",
            ModelName = "test-deployment",
            ApiKey = "test-key",
        });
        var client = new HttpClient(handler);
        var sut = new OpenAiCompatibleAnalystLlmClient(
            client,
            options,
            NullLogger<OpenAiCompatibleAnalystLlmClient>.Instance);

        var result = await sut.CompleteAsync(
            new AnalystLlmRequest(
                [new AnalystLlmMessage("user", "What are my positions?")],
                [new AnalystToolDefinition(
                    "get_positions",
                    "Get positions.",
                    JsonSerializer.SerializeToElement(new { type = "object" }))]),
            CancellationToken.None);

        result.ToolCalls.Should().ContainSingle().Which.Should().Be(
            new AnalystLlmToolCall("call-azure-1", "get_positions", "{}"));
        result.Usage.Should().Be(new AnalystTokenUsage(10, 4, 14));
        handler.RequestUri.Should().Be("/openai/responses?api-version=2025-04-01-preview");
        using var request = JsonDocument.Parse(handler.RequestBody!);
        request.RootElement.GetProperty("model").GetString().Should().Be("test-deployment");
        request.RootElement.GetProperty("input")[0].GetProperty("role").GetString().Should().Be("user");
        request.RootElement.GetProperty("tools")[0].GetProperty("name").GetString().Should().Be("get_positions");
    }

    private static OpenAiCompatibleAnalystLlmClient CreateSut(HttpMessageHandler handler)
    {
        var options = Options.Create(new LlmOptions
        {
            Provider = "Gemini",
            BaseUrl = "https://example.com/v1/",
            ModelName = "test-model",
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) };
        return new OpenAiCompatibleAnalystLlmClient(
            client,
            options,
            NullLogger<OpenAiCompatibleAnalystLlmClient>.Instance);
    }

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        public string? RequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            RequestUri = request.RequestUri?.PathAndQuery;
            return response;
        }
    }
}
