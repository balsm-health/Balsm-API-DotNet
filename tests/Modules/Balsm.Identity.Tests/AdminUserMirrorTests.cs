using Balsm.Identity.Domain;
using FluentAssertions;
using Xunit;

namespace Balsm.Identity.Tests;

public sealed class AdminUserMirrorTests
{
    [Fact]
    public void Create_WithPaddedMixedCaseEmail_NormalizesEmail()
    {
        var user = AdminUserMirror.Create("  Admin@Example.Test ", "Test Admin");

        user.Email.Should().Be("admin@example.test");
    }

    [Fact]
    public void Create_WithPaddedDisplayName_TrimsDisplayName()
    {
        var user = AdminUserMirror.Create("admin@example.test", "  Test Admin  ");

        user.DisplayName.Should().Be("Test Admin");
    }

    [Fact]
    public void Create_WithoutLocale_DefaultsToEnglishSystemAdmin()
    {
        var user = AdminUserMirror.Create("admin@example.test", "Test Admin");

        user.Should().BeEquivalentTo(new { Locale = "en", Role = "SystemAdmin", LastLoginAt = (DateTime?)null });
    }

    [Fact]
    public void Create_WithLocale_StoresLocale()
    {
        var user = AdminUserMirror.Create("admin@example.test", "Test Admin", "ar");

        user.Locale.Should().Be("ar");
    }

    [Fact]
    public void Create_Always_StampsPasswordChangedAt()
    {
        var before = DateTime.UtcNow;

        var user = AdminUserMirror.Create("admin@example.test", "Test Admin");

        user.PasswordChangedAt.Should().BeOnOrAfter(before);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankEmail_ThrowsArgumentException(string email)
    {
        var act = () => AdminUserMirror.Create(email, "Test Admin");

        act.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    [Fact]
    public void Create_WithNullEmail_ThrowsArgumentNullException()
    {
        var act = () => AdminUserMirror.Create(null!, "Test Admin");

        act.Should().Throw<ArgumentNullException>().WithParameterName("email");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankDisplayName_ThrowsArgumentException(string displayName)
    {
        var act = () => AdminUserMirror.Create("admin@example.test", displayName);

        act.Should().Throw<ArgumentException>().WithParameterName("displayName");
    }

    [Fact]
    public void UpdateProfile_WithValues_TrimsDisplayNameAndSetsLocale()
    {
        var user = AdminUserMirror.Create("admin@example.test", "Test Admin");

        user.UpdateProfile("  Renamed Admin ", "ar");

        user.Should().BeEquivalentTo(new { DisplayName = "Renamed Admin", Locale = "ar" });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateProfile_WithBlankDisplayName_ThrowsArgumentException(string displayName)
    {
        var user = AdminUserMirror.Create("admin@example.test", "Test Admin");

        var act = () => user.UpdateProfile(displayName, "ar");

        act.Should().Throw<ArgumentException>().WithParameterName("displayName");
    }

    [Fact]
    public void UpdateProfile_WithBlankDisplayName_LeavesProfileUnchanged()
    {
        var user = AdminUserMirror.Create("admin@example.test", "Test Admin");

        try { user.UpdateProfile(" ", "ar"); }
        catch (ArgumentException) { }

        user.Should().BeEquivalentTo(new { DisplayName = "Test Admin", Locale = "en" });
    }

    [Fact]
    public void RecordLogin_Always_SetsLastLoginAt()
    {
        var user = AdminUserMirror.Create("admin@example.test", "Test Admin");
        var before = DateTime.UtcNow;

        user.RecordLogin();

        user.LastLoginAt.Should().NotBeNull().And.BeOnOrAfter(before);
    }

    [Fact]
    public void RecordPasswordChange_Always_AdvancesPasswordChangedAt()
    {
        var user = AdminUserMirror.Create("admin@example.test", "Test Admin");
        var before = DateTime.UtcNow;

        user.RecordPasswordChange();

        user.PasswordChangedAt.Should().BeOnOrAfter(before);
    }
}
