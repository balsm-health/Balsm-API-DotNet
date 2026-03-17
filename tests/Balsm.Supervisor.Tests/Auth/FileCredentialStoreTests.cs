using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.Supervisor.Tests.Auth;

public class FileCredentialStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileCredentialStore _sut;

    public FileCredentialStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"balsm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var options = Options.Create(new SupervisorOptions
        {
            CredentialsPath = Path.Combine(_tempDir, "admin-credentials.json")
        });

        _sut = new FileCredentialStore(options, NullLogger<FileCredentialStore>.Instance);
    }

    [Fact]
    public async Task HasCredentials_WhenNoFile_ReturnsFalse()
    {
        var result = await _sut.HasCredentialsAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var creds = new AdminCredentials
        {
            Username = "admin",
            PasswordHash = "hash123",
            Salt = "salt123",
            CreatedAt = DateTime.UtcNow
        };

        await _sut.SaveCredentialsAsync(creds);
        var loaded = await _sut.LoadCredentialsAsync();

        loaded.Should().NotBeNull();
        loaded!.Username.Should().Be("admin");
        loaded.PasswordHash.Should().Be("hash123");
        loaded.Salt.Should().Be("salt123");
    }

    [Fact]
    public async Task HasCredentials_AfterSave_ReturnsTrue()
    {
        var creds = new AdminCredentials
        {
            Username = "admin",
            PasswordHash = "hash",
            Salt = "salt",
            CreatedAt = DateTime.UtcNow
        };

        await _sut.SaveCredentialsAsync(creds);
        var result = await _sut.HasCredentialsAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePassword_UpdatesHashAndSalt()
    {
        var creds = new AdminCredentials
        {
            Username = "admin",
            PasswordHash = "oldhash",
            Salt = "oldsalt",
            CreatedAt = DateTime.UtcNow
        };

        await _sut.SaveCredentialsAsync(creds);
        await _sut.UpdatePasswordAsync("newhash", "newsalt");

        var loaded = await _sut.LoadCredentialsAsync();
        loaded!.PasswordHash.Should().Be("newhash");
        loaded.Salt.Should().Be("newsalt");
        loaded.LastPasswordChange.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdatePassword_WhenNoFile_Throws()
    {
        var act = () => _sut.UpdatePasswordAsync("hash", "salt");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LoadCredentials_WhenNoFile_ReturnsNull()
    {
        var result = await _sut.LoadCredentialsAsync();
        result.Should().BeNull();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* Best effort cleanup */ }
    }
}
