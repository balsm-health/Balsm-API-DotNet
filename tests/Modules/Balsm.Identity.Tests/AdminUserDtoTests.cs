using Balsm.Identity.Application.DTOs;
using FluentAssertions;
using Xunit;

namespace Balsm.Identity.Tests;

public sealed class AdminUserDtoTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime PasswordChangedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        var first = new AdminUserDto(UserId, "admin@example.test", "Test Admin", "SystemAdmin", "en", null, PasswordChangedAt);
        var second = new AdminUserDto(UserId, "admin@example.test", "Test Admin", "SystemAdmin", "en", null, PasswordChangedAt);

        var equal = first.Equals(second);

        equal.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithValues_ExposesThem()
    {
        var lastLoginAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        var dto = new AdminUserDto(UserId, "admin@example.test", "Test Admin", "SystemAdmin", "ar", lastLoginAt, PasswordChangedAt);

        dto.Should().BeEquivalentTo(new
        {
            Id = UserId,
            Email = "admin@example.test",
            DisplayName = "Test Admin",
            Role = "SystemAdmin",
            Locale = "ar",
            LastLoginAt = (DateTime?)lastLoginAt,
            PasswordChangedAt,
        });
    }
}
