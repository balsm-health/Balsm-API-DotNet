using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.Supervisor.Tests.Services;

/// <summary>
/// Drives the tunnel service against a fake <c>cloudflared</c> shell script written to a
/// temp directory: it answers <c>--version</c>, prints a synthetic quick-tunnel URL on
/// stderr, and otherwise sleeps until killed. Nothing reaches the network.
/// </summary>
public sealed class CloudflareTunnelServiceTests : IDisposable
{
    private const string QuickTunnelUrl = "https://synthetic-test-tunnel.trycloudflare.com";
    private const string RegisteredTunnelUrl = "https://clinic.example.test";

    private readonly TempDirectory _temp = new();
    private readonly List<CloudflareTunnelService> _services = [];

    public void Dispose()
    {
        foreach (var service in _services)
        {
            service.Dispose();
        }

        _temp.Dispose();
    }

    private string WriteFakeCloudflared(string body, bool executable = true)
    {
        var path = _temp.Combine($"cloudflared-{Guid.NewGuid():N}");
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, executable
                ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                : UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return path;
    }

    private string WorkingFakeCloudflared() => WriteFakeCloudflared($$"""
        if [ "$1" = "--version" ]; then echo "cloudflared version 0.0.0-test"; exit 0; fi
        if [ "$2" = "--url" ]; then echo "INF +----+ {{QuickTunnelUrl}} +----+" 1>&2; fi
        echo "INF starting"
        exec sleep 30
        """);

    private async Task<CloudflareTunnelService> StartedSutAsync(SupervisorOptions options)
    {
        var sut = new CloudflareTunnelService(Options.Create(options), NullLogger<CloudflareTunnelService>.Instance);
        _services.Add(sut);
        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;
        return sut;
    }

    [Fact]
    public async Task ExecuteAsync_WhenExplicitPathDoesNotExist_ReportsNotInstalled()
    {
        var sut = await StartedSutAsync(new SupervisorOptions
        {
            CloudflaredPath = _temp.Combine("missing-cloudflared"),
            TunnelUrl = RegisteredTunnelUrl,
        });

        sut.IsCloudflaredInstalled.Should().BeFalse();
        sut.RegisteredUrl.Should().Be(RegisteredTunnelUrl);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBareNameIsNotOnPath_ReportsNotInstalled()
    {
        var sut = await StartedSutAsync(new SupervisorOptions
        {
            CloudflaredPath = $"balsm-no-such-binary-{Guid.NewGuid():N}",
        });

        sut.IsCloudflaredInstalled.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPathIsBlank_ReportsNotInstalled()
    {
        var sut = await StartedSutAsync(new SupervisorOptions { CloudflaredPath = " " });

        sut.IsCloudflaredInstalled.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenBareNameResolvesOnPathAndExitsZero_ReportsInstalled()
    {
        // "true" exists on PATH on every Unix and exits 0 whatever its arguments.
        var sut = await StartedSutAsync(new SupervisorOptions { CloudflaredPath = "true" });

        sut.IsCloudflaredInstalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenVersionCheckExitsNonZero_ReportsNotInstalled()
    {
        var sut = await StartedSutAsync(new SupervisorOptions { CloudflaredPath = WriteFakeCloudflared("exit 3") });

        sut.IsCloudflaredInstalled.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenFileIsNotExecutable_ReportsNotInstalled()
    {
        var sut = await StartedSutAsync(new SupervisorOptions
        {
            CloudflaredPath = WriteFakeCloudflared("exit 0", executable: false),
        });

        sut.IsCloudflaredInstalled.Should().BeFalse();
    }

    [Fact]
    public async Task StartTunnelAsync_WhenNotInstalled_SetsErrorMessage()
    {
        var sut = await StartedSutAsync(new SupervisorOptions { CloudflaredPath = _temp.Combine("missing") });

        await sut.StartTunnelAsync("quick", null, 5050);

        sut.ErrorMessage.Should().Be("cloudflared is not installed.");
        sut.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StartTunnelAsync_QuickTunnel_CapturesTrycloudflareUrlFromStderr()
    {
        var sut = await StartedSutAsync(new SupervisorOptions { CloudflaredPath = WorkingFakeCloudflared() });

        await sut.StartTunnelAsync("quick", null, 5050);

        sut.IsRunning.Should().BeTrue();
        sut.TunnelType.Should().Be("quick");
        sut.TunnelUrl.Should().Be(QuickTunnelUrl);
    }

    [Fact]
    public async Task StartTunnelAsync_NamedTunnelWithRegisteredUrl_UsesRegisteredUrlImmediately()
    {
        var sut = await StartedSutAsync(new SupervisorOptions
        {
            CloudflaredPath = WorkingFakeCloudflared(),
            TunnelUrl = RegisteredTunnelUrl,
        });

        await sut.StartTunnelAsync("named", "synthetic-token", 5050);

        sut.IsRunning.Should().BeTrue();
        sut.TunnelUrl.Should().Be(RegisteredTunnelUrl);
    }

    [Fact]
    public async Task StartTunnelAsync_WhenAlreadyRunning_StopsPreviousTunnelFirst()
    {
        var sut = await StartedSutAsync(new SupervisorOptions
        {
            CloudflaredPath = WorkingFakeCloudflared(),
            TunnelUrl = RegisteredTunnelUrl,
        });
        await sut.StartTunnelAsync("named", "synthetic-token", 5050);

        await sut.StartTunnelAsync("quick", null, 5050);

        sut.TunnelType.Should().Be("quick");
        sut.TunnelUrl.Should().Be(QuickTunnelUrl);
    }

    [Fact]
    public async Task StartTunnelAsync_WhenProcessExitsWithoutUrl_ReturnsWithoutUrl()
    {
        var path = WriteFakeCloudflared("""
            if [ "$1" = "--version" ]; then exit 0; fi
            echo "" 1>&2
            echo "no url here" 1>&2
            exit 1
            """);
        var sut = await StartedSutAsync(new SupervisorOptions { CloudflaredPath = path });

        await sut.StartTunnelAsync("quick", null, 5050);

        sut.TunnelUrl.Should().BeNull();
        sut.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task StartTunnelAsync_WhenBinaryDisappearsAfterDetection_SetsErrorMessage()
    {
        var path = WorkingFakeCloudflared();
        var sut = await StartedSutAsync(new SupervisorOptions { CloudflaredPath = path });
        File.Delete(path);

        await sut.StartTunnelAsync("quick", null, 5050);

        sut.ErrorMessage.Should().StartWith("Failed to start tunnel:");
        sut.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StopTunnelAsync_WhenRunning_KillsProcessAndResetsState()
    {
        var sut = await StartedSutAsync(new SupervisorOptions { CloudflaredPath = WorkingFakeCloudflared() });
        await sut.StartTunnelAsync("quick", null, 5050);

        await sut.StopTunnelAsync();

        sut.Should().BeEquivalentTo(new
        {
            IsRunning = false,
            TunnelUrl = (string?)null,
            TunnelType = (string?)null,
            ErrorMessage = (string?)null,
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithTokenAndTunnelEnabled_AutoStartsNamedTunnel()
    {
        var sut = await StartedSutAsync(new SupervisorOptions
        {
            CloudflaredPath = WorkingFakeCloudflared(),
            EnableTunnel = true,
            TunnelToken = "synthetic-token",
            TunnelUrl = RegisteredTunnelUrl,
        });

        var started = await Poll.UntilAsync(() => sut.IsRunning);

        started.Should().BeTrue();
        sut.TunnelType.Should().Be("named");
        sut.TunnelUrl.Should().Be(RegisteredTunnelUrl);
    }

    [Fact]
    public async Task StopAsync_WhenRunning_StopsTunnel()
    {
        var sut = await StartedSutAsync(new SupervisorOptions { CloudflaredPath = WorkingFakeCloudflared() });
        await sut.StartTunnelAsync("quick", null, 5050);

        await sut.StopAsync(CancellationToken.None);

        sut.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Dispose_WhenRunning_KillsProcessWithoutThrowing()
    {
        var sut = new CloudflareTunnelService(
            Options.Create(new SupervisorOptions { CloudflaredPath = WorkingFakeCloudflared() }),
            NullLogger<CloudflareTunnelService>.Instance);
        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;
        await sut.StartTunnelAsync("quick", null, 5050);

        var act = sut.Dispose;

        act.Should().NotThrow();
    }
}
