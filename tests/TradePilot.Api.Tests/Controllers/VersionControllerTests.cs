using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using TradePilot.Api.Models;
using TradePilot.Api.Tests.Infrastructure;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class VersionControllerTests : BaseControllerTests
{
    [TestMethod]
    public async Task GivenDeploymentMetadata_WhenGetVersion_ThenReturnsCurrentBuildDetails()
    {
        using var client = GetVersionClient();

        var response = await client.GetAsync("api/version?expectedVersion=sha-abcdef1&expectedCommit=abcdef1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiVersionResponse>();
        payload.Should().NotBeNull();
        payload!.Version.Should().Be("sha-abcdef1");
        payload.CommitSha.Should().Be("abcdef1");
        payload.BuildTimeUtc.Should().Be("2026-04-19T10:15:30Z");
        payload.RunId.Should().Be("123456789");
        payload.MatchesExpectedVersion.Should().BeTrue();
        payload.MatchesExpectedCommit.Should().BeTrue();
        payload.IsExpectedBuild.Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenMismatchedExpectedCommit_WhenGetVersion_ThenReportsNotCurrent()
    {
        using var client = GetVersionClient();

        var response = await client.GetAsync("api/version?expectedCommit=deadbeef");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiVersionResponse>();
        payload.Should().NotBeNull();
        payload!.CommitSha.Should().Be("abcdef1");
        payload.MatchesExpectedCommit.Should().BeFalse();
        payload.IsExpectedBuild.Should().BeFalse();
    }

    private HttpClient GetVersionClient()
    {
        return GetTestClient(authenticate: false);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Deployment:Version", "sha-abcdef1");
        builder.UseSetting("Deployment:CommitSha", "abcdef1");
        builder.UseSetting("Deployment:BuildTimeUtc", "2026-04-19T10:15:30Z");
        builder.UseSetting("Deployment:RunId", "123456789");
    }
}