using System.Text.Json;
using Balsm.Infrastructure.Audit;
using Balsm.Infrastructure.Platform;
using Balsm.SharedKernel.Events;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.Supervisor.Tests.Services;

// ServerStatusService reads/writes appsettings.Production.json and supervisor.pfx
// next to the running assembly, so these tests run serially and restore both files.
[Collection(SupervisorBaseDirectoryCollection.Name)]
public sealed class ServerStatusServiceTests : IDisposable
{
    private static readonly string ProductionConfigPath =
        Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json");

    private static readonly string CertPath =
        Path.Combine(AppContext.BaseDirectory, "supervisor.pfx");

    private readonly byte[]? _originalConfig;
    private readonly bool _certExisted;
    private readonly TempDirectory _temp = new();
    private readonly FakeHostApplicationLifetime _lifetime = new();
    private readonly ListLogger<ServerStatusService> _logger = new();

    public ServerStatusServiceTests()
    {
        _originalConfig = File.Exists(ProductionConfigPath) ? File.ReadAllBytes(ProductionConfigPath) : null;
        _certExisted = File.Exists(CertPath);
        File.Delete(ProductionConfigPath);
    }

    public void Dispose()
    {
        if (_originalConfig is null)
        {
            File.Delete(ProductionConfigPath);
        }
        else
        {
            File.WriteAllBytes(ProductionConfigPath, _originalConfig);
        }

        if (!_certExisted)
        {
            File.Delete(CertPath);
        }

        _temp.Dispose();
    }

    private ServerStatusService CreateSut(
        Dictionary<string, string?>? settings = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? [])
            .Build();
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        var provider = services.BuildServiceProvider();

        return new ServerStatusService(
            Options.Create(new SupervisorOptions()),
            configuration,
            _lifetime,
            provider.GetRequiredService<IServiceScopeFactory>(),
            _logger);
    }

    [Fact]
    public void GetStatus_WithDefaults_ReportsRunningLocalProcessOnDefaultPorts()
    {
        var sut = CreateSut();

        var status = sut.GetStatus();

        status.IsRunning.Should().BeTrue();
        status.Pid.Should().Be(Environment.ProcessId);
        status.Mode.Should().Be("local");
        status.HttpPort.Should().Be(5050);
        status.HttpsPort.Should().Be(5051);
        status.DbSizeBytes.Should().BeNull();
        status.Os.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetStatus_WhenCertificateIsAvailable_ReportsColonSeparatedSha256()
    {
        var sut = CreateSut();

        var status = sut.GetStatus();

        status.CertSha256.Should().MatchRegex("^([0-9A-F]{2}:){31}[0-9A-F]{2}$");
    }

    [Theory]
    [InlineData("http://0.0.0.0:8080", null, 8080, 8081)]
    [InlineData("http://localhost:80", null, 80, 443)]
    [InlineData("http://localhost:7000", "7443", 7000, 7443)]
    [InlineData("not a url", null, 5050, 5051)]
    public void GetStatus_WithServerUrls_DerivesHttpAndHttpsPorts(
        string urls, string? httpsPort, int expectedHttp, int expectedHttps)
    {
        var sut = CreateSut(new Dictionary<string, string?>
        {
            ["Server:Urls"] = urls,
            ["Server:HttpsPort"] = httpsPort,
        });

        var status = sut.GetStatus();

        status.HttpPort.Should().Be(expectedHttp);
        status.HttpsPort.Should().Be(expectedHttps);
    }

    [Theory]
    [InlineData("""{ "Server": { "Mode": "network" } }""", "network")]
    [InlineData("""{ "Server": { "Mode": "public" } }""", "public")]
    [InlineData("""{ "Server": { "Mode": "bogus", "Urls": "http://0.0.0.0:5050" } }""", "network")]
    [InlineData("""{ "Server": { "Urls": "http://localhost:5050" } }""", "local")]
    [InlineData("""{ "Server": { } }""", "local")]
    [InlineData("""{ "Other": 1 }""", "local")]
    [InlineData("{ not json", "local")]
    public void GetStatus_WithProductionConfig_ReadsMode(string json, string expectedMode)
    {
        File.WriteAllText(ProductionConfigPath, json);
        var sut = CreateSut();

        var status = sut.GetStatus();

        status.Mode.Should().Be(expectedMode);
    }

    [Fact]
    public void GetStatus_WhenPlatformDbFileExists_ReportsItsSize()
    {
        var dbPath = _temp.Combine("platform.db");
        File.WriteAllBytes(dbPath, new byte[1234]);
        var sut = CreateSut(configureServices: services =>
        {
            services.AddSingleton<IDomainEventDispatcher, NoOpDomainEventDispatcher>();
            services.AddDbContext<PlatformDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
        });

        var status = sut.GetStatus();

        status.DbSizeBytes.Should().Be(1234);
    }

    [Fact]
    public void GetStatus_WhenDbContextCannotBeResolved_LogsWarningAndReportsNoSize()
    {
        var sut = CreateSut(configureServices: services =>
            services.AddScoped<PlatformDbContext>(_ => throw new InvalidOperationException("db down")));

        var status = sut.GetStatus();

        status.DbSizeBytes.Should().BeNull();
        _logger.Contains(LogLevel.Warning, "Failed to read database file size").Should().BeTrue();
    }

    [Theory]
    [InlineData("network", "http://0.0.0.0:6000")]
    [InlineData("public", "http://0.0.0.0:6000")]
    [InlineData("local", "http://localhost:6000")]
    public async Task SwitchModeAsync_WritesProductionConfigAndStopsHost(string mode, string expectedUrls)
    {
        var sut = CreateSut();

        await sut.SwitchModeAsync(mode, 6000);

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(ProductionConfigPath));
        var server = doc.RootElement.GetProperty("Server");
        server.GetProperty("Urls").GetString().Should().Be(expectedUrls);
        server.GetProperty("Mode").GetString().Should().Be(mode);
        doc.RootElement.GetProperty("DeploymentMode").GetString().Should().Be("Standalone");
        _lifetime.StopApplicationCalls.Should().Be(1);
    }

    [Fact]
    public async Task SwitchModeAsync_WhenAuditWriterRegistered_WritesModeChangedEntry()
    {
        var auditWriter = new RecordingAuditLogWriter();
        var sut = CreateSut(configureServices: s => s.AddSingleton<IAuditLogWriter>(auditWriter));

        await sut.SwitchModeAsync("network", 6000);

        auditWriter.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Actor = "system",
            Module = "Mode",
            Action = "ModeChanged",
            DetailsJson = """{"mode":"network","port":6000}""",
        });
    }

    [Fact]
    public async Task SwitchModeAsync_WhenAuditWriterThrows_LogsWarningAndStillStopsHost()
    {
        var sut = CreateSut(configureServices: s => s.AddSingleton<IAuditLogWriter>(new ThrowingAuditLogWriter()));

        await sut.SwitchModeAsync("local", 6000);

        _logger.Contains(LogLevel.Warning, "Failed to write mode-change audit log").Should().BeTrue();
        _lifetime.StopApplicationCalls.Should().Be(1);
    }

    [Fact]
    public void RequestRestart_StopsHost()
    {
        var sut = CreateSut();

        sut.RequestRestart();

        _lifetime.StopApplicationCalls.Should().Be(1);
    }

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingAuditLogWriter : IAuditLogWriter
    {
        public List<AuditLog> Rows { get; } = [];

        public Task WriteAsync(AuditLog row, CancellationToken ct = default)
        {
            Rows.Add(row);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingAuditLogWriter : IAuditLogWriter
    {
        public Task WriteAsync(AuditLog row, CancellationToken ct = default)
            => throw new InvalidOperationException("audit store unavailable");
    }
}
