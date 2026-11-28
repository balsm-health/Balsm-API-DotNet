using Balsm.Deletion.Domain.Entities;
using Balsm.Deletion.Infrastructure.Data;
using Balsm.Deletion.Infrastructure.Jobs;
using Balsm.Deletion.Infrastructure.Services;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Balsm.Deletion.Tests;

/// Auth asks the registry before adopting a client-proposed account id.
public sealed class DeletedAccountRegistryTests : IDisposable
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private readonly ServiceProvider _sp;

    public DeletedAccountRegistryTests()
    {
        _conn.Open();
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IDomainEventDispatcher>());
        services.AddDbContext<DeletionDbContext>(o => o.UseSqlite(_conn));
        _sp = services.BuildServiceProvider();
        _sp.GetRequiredService<DeletionDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _sp.Dispose();
        _conn.Dispose();
    }

    [Fact]
    public async Task WasDeletedAsync_TrueOnlyForAnAccountWithADeletionLog()
    {
        var deleted = Guid.CreateVersion7();
        var db = _sp.GetRequiredService<DeletionDbContext>();
        db.DeletionLogs.Add(DeletionLog.Create(DeletionPurgeJob.ComputeHash(deleted), "EG"));
        await db.SaveChangesAsync();
        var registry = new DeletedAccountRegistry(db);

        (await registry.WasDeletedAsync(deleted)).Should().BeTrue();
        (await registry.WasDeletedAsync(Guid.CreateVersion7())).Should().BeFalse();
    }
}
