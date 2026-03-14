using Balsam.Supervisor.Auth;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsam.Supervisor.Tests.Auth;

public class AdminAuthServiceTests
{
    private readonly InMemoryCredentialStore _store = new();
    private readonly AdminAuthService _sut;

    public AdminAuthServiceTests()
    {
        _sut = new AdminAuthService(_store, NullLogger<AdminAuthService>.Instance);
    }

    [Fact]
    public async Task IsSetupComplete_WhenNoCredentials_ReturnsFalse()
    {
        var result = await _sut.IsSetupCompleteAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Setup_CreatesCredentials()
    {
        await _sut.SetupAsync("admin", "password123");

        var complete = await _sut.IsSetupCompleteAsync();
        complete.Should().BeTrue();
    }

    [Fact]
    public async Task Setup_RejectsDuplicate()
    {
        await _sut.SetupAsync("admin", "password123");

        var act = () => _sut.SetupAsync("admin", "password123");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already completed*");
    }

    [Fact]
    public async Task Setup_RejectsShortPassword()
    {
        var act = () => _sut.SetupAsync("admin", "short");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*at least 8*");
    }

    [Fact]
    public async Task Login_SucceedsWithCorrectCredentials()
    {
        await _sut.SetupAsync("admin", "password123");

        var result = await _sut.LoginAsync("admin", "password123");
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Login_FailsWithWrongPassword()
    {
        await _sut.SetupAsync("admin", "password123");

        var result = await _sut.LoginAsync("admin", "wrong");
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid");
    }

    [Fact]
    public async Task Login_FailsWithWrongUsername()
    {
        await _sut.SetupAsync("admin", "password123");

        var result = await _sut.LoginAsync("notadmin", "password123");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Login_LocksOutAfterFiveFailures()
    {
        await _sut.SetupAsync("admin", "password123");

        for (var i = 0; i < 5; i++)
        {
            await _sut.LoginAsync("admin", "wrong");
        }

        var result = await _sut.LoginAsync("admin", "password123");
        result.IsLockedOut.Should().BeTrue();
        result.LockoutRemaining.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_SucceedsAfterLockoutExpires()
    {
        await _sut.SetupAsync("admin", "password123");

        // Create 5 failures to trigger lockout
        for (var i = 0; i < 5; i++)
        {
            await _sut.LoginAsync("admin", "wrong");
        }

        // Manually expire the lockout
        var creds = await _store.LoadCredentialsAsync();
        creds!.LockoutEnd = DateTime.UtcNow.AddMinutes(-1);
        await _store.SaveCredentialsAsync(creds);

        var result = await _sut.LoginAsync("admin", "password123");
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_VerifiesCurrentPassword()
    {
        await _sut.SetupAsync("admin", "password123");

        var act = () => _sut.ChangePasswordAsync("wrongcurrent", "newpassword123");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ChangePassword_RejectsShortNewPassword()
    {
        await _sut.SetupAsync("admin", "password123");

        var act = () => _sut.ChangePasswordAsync("password123", "short");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ChangePassword_AllowsLoginWithNewPassword()
    {
        await _sut.SetupAsync("admin", "password123");
        await _sut.ChangePasswordAsync("password123", "newpassword456");

        var oldResult = await _sut.LoginAsync("admin", "password123");
        oldResult.IsSuccess.Should().BeFalse();

        var newResult = await _sut.LoginAsync("admin", "newpassword456");
        newResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void HashPassword_ProducesDeterministicOutput()
    {
        var salt = new byte[32];
        var hash1 = AdminAuthService.HashPassword("test", salt);
        var hash2 = AdminAuthService.HashPassword("test", salt);

        hash1.Should().Equal(hash2);
    }

    [Fact]
    public void HashPassword_DifferentSaltsProduceDifferentHashes()
    {
        var salt1 = new byte[32];
        var salt2 = new byte[32];
        salt2[0] = 1;

        var hash1 = AdminAuthService.HashPassword("test", salt1);
        var hash2 = AdminAuthService.HashPassword("test", salt2);

        hash1.Should().NotEqual(hash2);
    }
}

/// <summary>
/// In-memory credential store for unit testing.
/// </summary>
public sealed class InMemoryCredentialStore : ICredentialStore
{
    private AdminCredentials? _creds;

    public Task<bool> HasCredentialsAsync(CancellationToken ct = default)
        => Task.FromResult(_creds is not null);

    public Task SaveCredentialsAsync(AdminCredentials credentials, CancellationToken ct = default)
    {
        _creds = credentials;
        return Task.CompletedTask;
    }

    public Task<AdminCredentials?> LoadCredentialsAsync(CancellationToken ct = default)
        => Task.FromResult(_creds);

    public Task UpdatePasswordAsync(string passwordHash, string salt, CancellationToken ct = default)
    {
        if (_creds is null) throw new InvalidOperationException("No credentials");
        _creds.PasswordHash = passwordHash;
        _creds.Salt = salt;
        _creds.LastPasswordChange = DateTime.UtcNow;
        return Task.CompletedTask;
    }
}
