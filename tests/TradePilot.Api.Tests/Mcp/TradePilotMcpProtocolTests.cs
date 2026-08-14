using System.Text;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Client;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.MarketAnalysis.Models;
using TradePilot.Application.MarketAnalysis.Queries;

namespace TradePilot.Api.Tests.Mcp;

[TestClass]
public sealed class TradePilotMcpProtocolTests : BaseControllerTests
{
    private readonly Mock<ISender> _sender = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Mcp:Enabled", "true");
        builder.UseSetting("Mcp:Path", "/mcp");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<ISender>();
        services.AddSingleton(_sender.Object);
    }

    [TestMethod]
    public async Task GivenAuthenticatedMcpClient_WhenDiscoveringAndInvoking_ThenReadOnlyStructuredToolWorks()
    {
        var expected = CreateAnalysis();
        _sender
            .Setup(sender => sender.Send(
                It.Is<AnalyseMarketQuery>(query =>
                    query.Symbol == "BTC"
                    && query.Timeframe == "4h"
                    && query.Exchange == Exchange.Hyperliquid),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        using var httpClient = GetTestClient();
        await using var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "mcp"),
                EnableStandaloneGetStream = false,
            },
            httpClient);
        await using var client = await McpClient.CreateAsync(transport);

        var tools = await client.ListToolsAsync();
        tools.Select(tool => tool.Name).Should().BeEquivalentTo(
        [
            "get_market_snapshot",
            "analyse_market",
            "analyse_market_multi_timeframe",
            "get_account_summary",
            "get_positions",
            "get_open_orders",
            "get_recent_fills",
        ]);

        var result = await client.CallToolAsync(
            "analyse_market",
            new Dictionary<string, object?>
            {
                ["symbol"] = "BTC",
                ["timeframe"] = "4h",
            });

        result.IsError.Should().NotBeTrue();
        result.StructuredContent.Should().NotBeNull();
        var structured = result.StructuredContent!.Value;
        structured.GetProperty("trend").GetString().Should().Be("bullish");
        structured.GetProperty("marketStructure").GetString().Should().Be("higher_high_higher_low");
        structured.GetProperty("indicators").GetProperty("ema200").GetDecimal().Should().Be(55_000m);
    }

    [TestMethod]
    public async Task GivenAnonymousClient_WhenCallingMcpEndpoint_ThenAuthenticationIsRequired()
    {
        using var httpClient = GetTestClient(authenticate: false);
        using var request = new HttpRequestMessage(HttpMethod.Post, "mcp")
        {
            Content = new StringContent(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2026-07-28");

        var response = await httpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static MarketAnalysisResult CreateAnalysis()
    {
        return new MarketAnalysisResult(
            "BTC",
            "4h",
            new DateTimeOffset(2026, 8, 14, 8, 0, 0, TimeSpan.Zero),
            60_000m,
            new MarketIndicatorValues(59_000m, 58_000m, 55_000m, 60m, 1_200m, 2m, 1m, 2m, 9m),
            MarketTrend.Bullish,
            MarketMomentum.Bullish,
            VolatilityRegime.Normal,
            MarketStructure.HigherHighHigherLow,
            61_000m,
            57_000m);
    }
}
