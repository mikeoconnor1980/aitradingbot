using System.Text.Json.Serialization;

namespace TradePilot.Application.Agent.Models;

/// <summary>
/// Describes a single installer-related release artifact stored in Blob Storage.
/// </summary>
public sealed class InstallerReleaseFile
{
    [JsonPropertyName("filename")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("blobName")]
    public string BlobName { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;
}