namespace TradePilot.Application.Agent.Models;

/// <summary>
/// Configuration for serving installer files from disk. Bound from appsettings "Installer" section.
/// </summary>
public sealed class InstallerOptions
{
    public const string SectionName = "Installer";

    /// <summary>
    /// Absolute or relative path to the directory containing installer artifacts
    /// (e.g. TradePilot-ExecutionAgent-v{version}-Setup.exe).
    /// </summary>
    public string InstallerDirectory { get; set; } = string.Empty;
}
