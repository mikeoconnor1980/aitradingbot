using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TradePilot.Api.Configuration;
using TradePilot.Api.Models;

namespace TradePilot.Api.Controllers;

[ApiController]
[Route("api/version")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class VersionController : ControllerBase
{
    private readonly DeploymentVersionOptions _deployment;
    private readonly IHostEnvironment _hostEnvironment;

    public VersionController(
        IOptions<DeploymentVersionOptions> deploymentOptions,
        IHostEnvironment hostEnvironment)
    {
        _deployment = deploymentOptions.Value;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiVersionResponse), StatusCodes.Status200OK)]
    public ActionResult<ApiVersionResponse> GetVersion(
        [FromQuery] string? expectedVersion = null,
        [FromQuery] string? expectedCommit = null)
    {
        var assembly = typeof(Program).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        var version = FirstNonEmpty(
            _deployment.Version,
            assembly.GetName().Version?.ToString(),
            informationalVersion,
            "unknown");

        var commitSha = FirstNonEmpty(
            _deployment.CommitSha,
            ExtractCommitFromInformationalVersion(informationalVersion),
            "unknown");

        var buildTimeUtc = FirstNonEmpty(_deployment.BuildTimeUtc, "unknown");
        var runId = FirstNonEmpty(_deployment.RunId, "unknown");

        bool? matchesExpectedVersion = string.IsNullOrWhiteSpace(expectedVersion)
            ? null
            : string.Equals(version, expectedVersion.Trim(), StringComparison.OrdinalIgnoreCase);

        bool? matchesExpectedCommit = string.IsNullOrWhiteSpace(expectedCommit)
            ? null
            : string.Equals(commitSha, expectedCommit.Trim(), StringComparison.OrdinalIgnoreCase);

        bool? isExpectedBuild = null;
        if (matchesExpectedVersion.HasValue || matchesExpectedCommit.HasValue)
        {
            isExpectedBuild = (matchesExpectedVersion ?? true) && (matchesExpectedCommit ?? true);
        }

        return Ok(new ApiVersionResponse
        {
            Version = version,
            InformationalVersion = informationalVersion,
            CommitSha = commitSha,
            BuildTimeUtc = buildTimeUtc,
            RunId = runId,
            EnvironmentName = _hostEnvironment.EnvironmentName,
            MatchesExpectedVersion = matchesExpectedVersion,
            MatchesExpectedCommit = matchesExpectedCommit,
            IsExpectedBuild = isExpectedBuild,
        });
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string? ExtractCommitFromInformationalVersion(string informationalVersion)
    {
        var separatorIndex = informationalVersion.LastIndexOf('+');
        if (separatorIndex < 0 || separatorIndex == informationalVersion.Length - 1)
        {
            return null;
        }

        return informationalVersion[(separatorIndex + 1)..];
    }
}