namespace Balsm.Identity.Application.DTOs;

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string Locale,
    DateTime? LastLoginAt,
    DateTime PasswordChangedAt);
