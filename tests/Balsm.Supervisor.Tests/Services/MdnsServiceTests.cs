using Balsm.Entity.Application.DTOs;
using Balsm.SharedKernel.Results;
using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Services;
using Balsm.Supervisor.Tests.Auth;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.Supervisor.Tests.Services;

/// <summary>
/// Covers instance naming and failure handling. The status lookup that precedes the
/// multicast socket is made to fail, so no test ever opens a socket or broadcasts.
/// </summary>
public sealed class MdnsServiceTests
{
    private static readonly DateTime FixedTime = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid WorkspaceId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly InMemoryCredentialStore _credentials = new();
    private readonly ListLogger<MdnsService> _logger = new();

    private MdnsService CreateSut(
        string? serverId = "abcdef0123456789",
        bool enableMdns = true,
        FakeWorkspaceMediator? mediator = null)
    {
        var options = Options.Create(new SupervisorOptions { ServerId = serverId, EnableMdns = enableMdns });
        var services = new ServiceCollection();
        if (mediator is not null)
        {
            services.AddSingleton<IMediator>(mediator);
        }

        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var status = new ServerStatusService(
            options,
            new ThrowingConfiguration(),
            new FakeHostApplicationLifetime(),
            scopeFactory,
            NullLogger<ServerStatusService>.Instance);

        return new MdnsService(
            options,
            new NetworkDiscoveryService(NullLogger<NetworkDiscoveryService>.Instance),
            new AdminAuthService(_credentials, [new Pbkdf2Hasher()], NullLogger<AdminAuthService>.Instance),
            status,
            scopeFactory,
            _logger);
    }

    private Task CompleteSetupAsync() => _credentials.SaveCredentialsAsync(new AdminCredentials
    {
        Username = "admin@example.test",
        PasswordHash = "synthetic-hash",
        Salt = "synthetic-salt",
        CreatedAt = FixedTime,
    });

    private static async Task RunAsync(MdnsService sut)
    {
        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_DoesNotRegister()
    {
        var sut = CreateSut(enableMdns: false);

        await RunAsync(sut);

        sut.IsRegistered.Should().BeFalse();
        sut.MdnsName.Should().BeNull();
        _logger.Contains(LogLevel.Information, "mDNS is disabled").Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_BeforeSetup_UsesSetupNameWithShortServerId()
    {
        var sut = CreateSut(serverId: "abcdef0123456789");

        await RunAsync(sut);

        sut.MdnsName.Should().Be("balsm-setup-abcdef01");
    }

    [Fact]
    public async Task ExecuteAsync_BeforeSetupWithoutServerId_UsesLocalSuffix()
    {
        var sut = CreateSut(serverId: null);

        await RunAsync(sut);

        sut.MdnsName.Should().Be("balsm-setup-local");
    }

    [Fact]
    public async Task ExecuteAsync_AfterSetup_UsesWorkspaceSlug()
    {
        await CompleteSetupAsync();
        var mediator = new FakeWorkspaceMediator(
            Result.Success(new WorkspaceDto(WorkspaceId, "Clinic One", "clinic-one", "Active", "en")));
        var sut = CreateSut(mediator: mediator);

        await RunAsync(sut);

        sut.MdnsName.Should().Be("balsm-clinic-one");
        mediator.SendCalls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_AfterSetupWhenWorkspaceLookupFails_FallsBackToDefaultSlug()
    {
        await CompleteSetupAsync();
        var mediator = new FakeWorkspaceMediator(Result.Failure<WorkspaceDto>(Error.NotFound));
        var sut = CreateSut(mediator: mediator);

        await RunAsync(sut);

        sut.MdnsName.Should().Be("balsm-balsm");
    }

    [Fact]
    public async Task ExecuteAsync_WhenStartupFails_LogsWarningAndStaysUnregistered()
    {
        var sut = CreateSut();

        await RunAsync(sut);

        sut.IsRegistered.Should().BeFalse();
        _logger.Contains(LogLevel.Warning, "Failed to start mDNS").Should().BeTrue();
    }

    [Fact]
    public async Task RestartAsync_WhenNothingStarted_ReattemptsStartup()
    {
        var sut = CreateSut(serverId: "1234");

        await sut.RestartAsync();

        sut.MdnsName.Should().Be("balsm-setup-1234");
        sut.IsRegistered.Should().BeFalse();
    }

    [Fact]
    public void Dispose_WhenNeverStarted_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = sut.Dispose;

        act.Should().NotThrow();
    }
}
