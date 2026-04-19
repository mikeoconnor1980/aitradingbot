namespace TradePilot.Api.Models;

public sealed class ApiVersionResponse
{
    public string Version { get; init; } = string.Empty;

    public string InformationalVersion { get; init; } = string.Empty;

    public string CommitSha { get; init; } = string.Empty;

    public string BuildTimeUtc { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string EnvironmentName { get; init; } = string.Empty;

    public bool? MatchesExpectedVersion { get; init; }

    public bool? MatchesExpectedCommit { get; init; }

    public bool? IsExpectedBuild { get; init; }
}