namespace TradePilot.Application.Administration.Models;

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    Guid? UserId,
    string? DisplayName,
    bool HasRegisteredAccount,
    long CreatedAtUtc);