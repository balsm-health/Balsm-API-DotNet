using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Services;
using Balsm.Supervisor.Tests.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.Supervisor.Tests.Services;

/// <summary>
/// Covers the returning-install boot path. The first-run path opens the system
/// browser (an OS boundary excluded from coverage), so it is never driven here.
/// </summary>
public sealed class FirstRunServiceTests : IDisposable
{
    private static readonly DateTime FixedTime = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly TempDirectory _temp = new();
    private readonly ListLogger<FirstRunService> _logger = new();
    private readonly InMemoryCredentialStore _credentials = new();

    public FirstRunServiceTests()
    {
        File.WriteAllText(SentinelPath, FixedTime.ToString("O"));
        _credentials.SaveCredentialsAsync(new AdminCredentials
        {
            Username = "admin@example.test",
            PasswordHash = "synthetic-hash",
            Salt = "synthetic-salt",
            CreatedAt = FixedTime,
        }).GetAwaiter().GetResult();
    }

    private string SentinelPath => _temp.Combine(".first-run-done");

    private string ConnectionInfoPath => _temp.Combine("connection-info.txt");

    public void Dispose() => _temp.Dispose();

    private FirstRunService CreateSut(IServer server, string? connectionInfoPath = null)
    {
        var options = Options.Create(new SupervisorOptions
        {
            FirstRunSentinelPath = SentinelPath,
            ConnectionInfoPath = connectionInfoPath ?? ConnectionInfoPath,
        });
        var network = new NetworkDiscoveryService(NullLogger<NetworkDiscoveryService>.Instance);
        var connectionInfo = new ConnectionInfoService(options, network, NullLogger<ConnectionInfoService>.Instance);
        var provider = new ServiceCollection()
            .AddSingleton(new AdminAuthService(
                _credentials, [new Pbkdf2Hasher()], NullLogger<AdminAuthService>.Instance))
            .BuildServiceProvider();

        return new FirstRunService(options, connectionInfo, network, server, provider, _logger);
    }

    private static async Task RunToCompletionAsync(FirstRunService sut)
    {
        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;
    }

    [Fact]
    public async Task ExecuteAsync_WhenSetupAlreadyDone_LogsConsoleBannerAndWritesConnectionInfoForBoundPort()
    {
        var sut = CreateSut(new FakeServer("not-a-uri", "http://localhost:6060"));

        await RunToCompletionAsync(sut);

        var info = await File.ReadAllTextAsync(ConnectionInfoPath);
        info.Should().Contain("http://localhost:6060/api/v1");
        _logger.Contains(LogLevel.Information, "Admin Panel:").Should().BeTrue();
        _logger.Contains(LogLevel.Information, "Opening setup page").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenServerHasNoAddresses_FallsBackToPort5050()
    {
        var sut = CreateSut(new FakeServer());

        await RunToCompletionAsync(sut);

        (await File.ReadAllTextAsync(ConnectionInfoPath)).Should().Contain("http://localhost:5050/api/v1");
    }

    [Fact]
    public async Task ExecuteAsync_WhenServerFeaturesThrow_FallsBackToPort5050()
    {
        var sut = CreateSut(new ThrowingFeaturesServer());

        await RunToCompletionAsync(sut);

        (await File.ReadAllTextAsync(ConnectionInfoPath)).Should().Contain("http://localhost:5050/api/v1");
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionInfoCannotBeWritten_LogsWarning()
    {
        var unwritable = Path.Combine(_temp.Path, "missing-dir", "connection-info.txt");
        var sut = CreateSut(new FakeServer("http://localhost:6060"), unwritable);

        await RunToCompletionAsync(sut);

        _logger.Contains(LogLevel.Warning, "Failed to write connection-info.txt").Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStoppedDuringStartupDelay_ReturnsWithoutWritingAnything()
    {
        var sut = CreateSut(new FakeServer("http://localhost:6060"));
        await sut.StartAsync(CancellationToken.None);
        // ExecuteAsync is scheduled on the thread pool; give it time to reach its
        // 2 s start-up delay so the stop lands inside that delay.
        await Task.Delay(300);

        await sut.StopAsync(CancellationToken.None);

        File.Exists(ConnectionInfoPath).Should().BeFalse();
        _logger.Entries.Should().BeEmpty();
    }
}
