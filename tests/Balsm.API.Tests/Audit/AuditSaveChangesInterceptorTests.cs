using Balsm.API.Tests.Platform;
using Balsm.Infrastructure.Audit;
using Balsm.Infrastructure.Platform;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.Tests.Audit;

/// <summary>
/// The interceptor reads the change tracker after the save. These tests save
/// with <c>acceptAllChangesOnSuccess: false</c> so the tracked states are still
/// there to read — see the report on why a default save records nothing.
/// </summary>
public sealed class AuditSaveChangesInterceptorTests : IDisposable
{
    private readonly CapturingWriter _writer = new();
    private readonly PlatformDbHarness _harness;

    public AuditSaveChangesInterceptorTests()
    {
        var services = new ServiceCollection().AddSingleton<IAuditLogWriter>(_writer).BuildServiceProvider();
        _harness = new PlatformDbHarness(new AuditSaveChangesInterceptor(services));
    }

    public void Dispose()
    {
        _harness.Dispose();
        AuditContext.Current = null;
    }

    private sealed class CapturingWriter : IAuditLogWriter
    {
        public List<AuditLog> Rows { get; } = [];

        public Task WriteAsync(AuditLog row, CancellationToken ct = default)
        {
            Rows.Add(row);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SavedChanges_AddedEntity_WritesACreatedRowWithTheAuditContext()
    {
        AuditContext.Current = new AuditContextValue("admin@example.test", "10.0.0.1", "corr-1");
        var entry = new ServerConfigEntry { Key = "k", Value = "v" };
        _harness.Db.ServerConfigs.Add(entry);

        await _harness.Db.SaveChangesAsync(acceptAllChangesOnSuccess: false);

        _writer.Rows.Single().Should().BeEquivalentTo(new
        {
            Action = "Created",
            Actor = "admin@example.test",
            SourceIp = "10.0.0.1",
            CorrelationId = "corr-1",
            Module = "Platform",
            TargetType = nameof(ServerConfigEntry),
            TargetId = entry.Id.ToString(),
        });
    }

    [Fact]
    public async Task SavedChanges_ModifiedEntity_WritesAnUpdatedRow()
    {
        var entry = _harness.Db.ServerConfigs.First();
        entry.Value = "changed";

        await _harness.Db.SaveChangesAsync(acceptAllChangesOnSuccess: false);

        _writer.Rows.Single().Action.Should().Be("Updated");
    }

    [Fact]
    public async Task SavedChanges_SoftDeletedEntity_WritesASoftDeletedRow()
    {
        var entry = _harness.Db.ServerConfigs.First();
        entry.IsDeleted = true;

        await _harness.Db.SaveChangesAsync(acceptAllChangesOnSuccess: false);

        _writer.Rows.Single().Action.Should().Be("SoftDeleted");
    }

    [Fact]
    public async Task SavedChanges_RemovedEntity_WritesADeletedRow()
    {
        _harness.Db.ServerConfigs.Remove(_harness.Db.ServerConfigs.First());

        await _harness.Db.SaveChangesAsync(acceptAllChangesOnSuccess: false);

        _writer.Rows.Single().Action.Should().Be("Deleted");
    }

    [Fact]
    public async Task SavedChanges_NoAuditContext_LeavesTheActorEmpty()
    {
        AuditContext.Current = null;
        _harness.Db.ServerConfigs.Add(new ServerConfigEntry { Key = "k2", Value = "v" });

        await _harness.Db.SaveChangesAsync(acceptAllChangesOnSuccess: false);

        _writer.Rows.Single().Actor.Should().BeNull();
    }

    [Fact]
    public async Task SavedChanges_NothingChanged_WritesNothing()
    {
        await _harness.Db.SaveChangesAsync(acceptAllChangesOnSuccess: false);

        _writer.Rows.Should().BeEmpty();
    }
}
