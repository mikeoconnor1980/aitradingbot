using System.ComponentModel.DataAnnotations;

namespace TradePilot.Api.Infrastructure;

public sealed class AdminAuthorizationOptions
{
    public const string SectionName = "Admin";

    [Required]
    public string[] Emails { get; init; } = [];
}