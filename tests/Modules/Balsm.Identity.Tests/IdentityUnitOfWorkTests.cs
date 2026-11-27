using Balsm.Identity.Domain;
using Balsm.Identity.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.Identity.Tests;

public sealed class IdentityUnitOfWorkTests : IDisposable
{
    private readonly IdentityTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task SaveChangesAsync_WithPendingInsert_ReturnsAffectedRowCount()
    {
        using var scope = _host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAdminUserMirrorRepository>()
            .AddAsync(AdminUserMirror.Create("admin@example.test", "Test Admin"));
        var uow = scope.ServiceProvider.GetRequiredService<IIdentityUnitOfWork>();

        var affected = await uow.SaveChangesAsync();

        affected.Should().Be(1);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNoPendingChanges_ReturnsZero()
    {
        using var scope = _host.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IIdentityUnitOfWork>();

        var affected = await uow.SaveChangesAsync();

        affected.Should().Be(0);
    }

    [Fact]
    public async Task SaveChangesAsync_WithPendingInsert_MakesRowVisibleToOtherScopes()
    {
        using (var scope = _host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAdminUserMirrorRepository>()
                .AddAsync(AdminUserMirror.Create("admin@example.test", "Test Admin"));
            await scope.ServiceProvider.GetRequiredService<IIdentityUnitOfWork>().SaveChangesAsync();
        }
        using var readScope = _host.Services.CreateScope();

        var all = await readScope.ServiceProvider.GetRequiredService<IAdminUserMirrorRepository>().GetAllAsync();

        all.Should().ContainSingle(u => u.Email == "admin@example.test");
    }
}
