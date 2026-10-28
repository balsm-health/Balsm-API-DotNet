using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.Supervisor.Tests.Services;

// LocalCliTokenService keeps the token in a static field, so these tests run serially.
[Collection(SupervisorBaseDirectoryCollection.Name)]
public sealed class LocalCliTokenServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ListLogger<LocalCliTokenService> _logger = new();

    public void Dispose() => _temp.Dispose();

    private LocalCliTokenService CreateSut(string tokenPath)
        => new(Options.Create(new SupervisorOptions { LocalCliTokenPath = tokenPath }), _logger);

    [Fact]
    public async Task StartAsync_WhenDirectoryIsMissing_WritesHexTokenAndExposesIt()
    {
        var tokenPath = Path.Combine(_temp.Path, "var", "local-cli.token");
        var sut = CreateSut(tokenPath);

        await sut.StartAsync(CancellationToken.None);

        var written = await File.ReadAllTextAsync(tokenPath);
        written.Should().MatchRegex("^[0-9a-f]{64}$");
        LocalCliTokenService.CurrentToken.Should().Be(written);
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_OnUnix_MakesTokenFileOwnerReadOnly()
    {
        var tokenPath = _temp.Combine("local-cli.token");
        var sut = CreateSut(tokenPath);

        await sut.StartAsync(CancellationToken.None);

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(tokenPath).Should().Be(UnixFileMode.UserRead);
        }

        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenReadOnlyTokenFromPriorRunExists_ReplacesIt()
    {
        var tokenPath = _temp.Combine("local-cli.token");
        var first = CreateSut(tokenPath);
        await first.StartAsync(CancellationToken.None);
        var firstToken = LocalCliTokenService.CurrentToken;
        var second = CreateSut(tokenPath);

        await second.StartAsync(CancellationToken.None);

        var written = await File.ReadAllTextAsync(tokenPath);
        written.Should().NotBe(firstToken).And.Be(LocalCliTokenService.CurrentToken);
        await second.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_AfterStart_ClearsTokenAndDeletesFile()
    {
        var tokenPath = _temp.Combine("local-cli.token");
        var sut = CreateSut(tokenPath);
        await sut.StartAsync(CancellationToken.None);

        await sut.StopAsync(CancellationToken.None);

        LocalCliTokenService.CurrentToken.Should().BeNull();
        File.Exists(tokenPath).Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WhenFileWasNeverWritten_DoesNotThrow()
    {
        var sut = CreateSut(_temp.Combine("never-written.token"));

        var act = () => sut.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_WhenTokenDirectoryCannotBeCreated_LogsWarningWithoutThrowing()
    {
        var blocker = _temp.Combine("not-a-directory");
        await File.WriteAllTextAsync(blocker, "x");
        var sut = CreateSut(Path.Combine(blocker, "local-cli.token"));

        await sut.StartAsync(CancellationToken.None);

        _logger.Contains(LogLevel.Warning, "Failed to write local CLI token").Should().BeTrue();
    }
}
