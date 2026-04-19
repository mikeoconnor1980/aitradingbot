namespace TradePilot.Api.Configuration;

public sealed class DeploymentVersionOptions
{
    public const string SectionName = "Deployment";

    public string? Version { get; set; }

    public string? CommitSha { get; set; }

    public string? BuildTimeUtc { get; set; }

    public string? RunId { get; set; }
}