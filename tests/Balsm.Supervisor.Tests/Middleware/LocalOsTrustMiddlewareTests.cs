using System.Net;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Middleware;
using Balsm.Supervisor.Services;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.Supervisor.Tests.Middleware;

[Collection(ProcessStateCollection.Name)]
public sealed class LocalOsTrustMiddlewareTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"balsm-localcli-{Guid.NewGuid():N}");
    private readonly LocalCliTokenService _tokenService;
    private bool _nextCalled;

    public LocalOsTrustMiddlewareTests()
    {
        Directory.CreateDirectory(_tempDir);
        _tokenService = new LocalCliTokenService(
            Options.Create(new SupervisorOptions { LocalCliTokenPath = Path.Combine(_tempDir, "local-cli.token") }),
            NullLogger<LocalCliTokenService>.Instance);
    }

    [Fact]
    public async Task InvokeAsync_LoopbackWithMatchingToken_MarksRequestLocal()
    {
        await _tokenService.StartAsync(CancellationToken.None);
        var context = CreateContext(IPAddress.Loopback, LocalCliTokenService.CurrentToken);

        await CreateSut().InvokeAsync(context);

        context.Items.Should().ContainKey("LocalCli");
        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_IPv6LoopbackWithMatchingToken_MarksRequestLocal()
    {
        await _tokenService.StartAsync(CancellationToken.None);
        var context = CreateContext(IPAddress.IPv6Loopback, LocalCliTokenService.CurrentToken);

        await CreateSut().InvokeAsync(context);

        context.Items.Should().ContainKey("LocalCli");
    }

    [Fact]
    public async Task InvokeAsync_RemoteAddressWithMatchingToken_DoesNotTrust()
    {
        await _tokenService.StartAsync(CancellationToken.None);
        var context = CreateContext(IPAddress.Parse("192.0.2.10"), LocalCliTokenService.CurrentToken);

        await CreateSut().InvokeAsync(context);

        context.Items.Should().NotContainKey("LocalCli");
        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_UnknownRemoteAddress_DoesNotTrust()
    {
        await _tokenService.StartAsync(CancellationToken.None);
        var context = CreateContext(null, LocalCliTokenService.CurrentToken);

        await CreateSut().InvokeAsync(context);

        context.Items.Should().NotContainKey("LocalCli");
    }

    [Theory]
    [InlineData("wrong-token")]
    [InlineData(null)]
    public async Task InvokeAsync_LoopbackWithWrongOrMissingToken_DoesNotTrust(string? header)
    {
        await _tokenService.StartAsync(CancellationToken.None);
        var context = CreateContext(IPAddress.Loopback, header);

        await CreateSut().InvokeAsync(context);

        context.Items.Should().NotContainKey("LocalCli");
    }

    [Fact]
    public async Task InvokeAsync_LoopbackWithTokenInDifferentCase_DoesNotTrust()
    {
        await _tokenService.StartAsync(CancellationToken.None);
        var context = CreateContext(IPAddress.Loopback, LocalCliTokenService.CurrentToken!.ToUpperInvariant());

        await CreateSut().InvokeAsync(context);

        context.Items.Should().NotContainKey("LocalCli");
    }

    [Fact]
    public async Task InvokeAsync_WhenNoTokenIssued_DoesNotTrustEmptyHeader()
    {
        await _tokenService.StopAsync(CancellationToken.None);
        var context = CreateContext(IPAddress.Loopback, "");

        await CreateSut().InvokeAsync(context);

        context.Items.Should().NotContainKey("LocalCli");
        _nextCalled.Should().BeTrue();
    }

    public void Dispose()
    {
        _tokenService.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* Best effort cleanup */ }
    }

    private LocalOsTrustMiddleware CreateSut() => new(_ =>
    {
        _nextCalled = true;
        return Task.CompletedTask;
    });

    private static DefaultHttpContext CreateContext(IPAddress? remote, string? token)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remote;
        if (token is not null)
            context.Request.Headers["X-Balsm-Local-Token"] = token;
        return context;
    }
}
