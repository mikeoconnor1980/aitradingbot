namespace TradePilot.Application.Agent.Models;

/// <summary>
/// Metadata about a single installer file in the store.
/// </summary>
public sealed record InstallerFileInfo(bool Exists, long? SizeBytes);
