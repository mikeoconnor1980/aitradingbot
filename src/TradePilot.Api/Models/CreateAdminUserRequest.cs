using System.ComponentModel.DataAnnotations;

namespace TradePilot.Api.Models;

public sealed class CreateAdminUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
}