using Balsm.Identity.Domain;
using Balsm.Identity.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.Identity.Tests;

public sealed class AdminUserMirrorRepositoryTests : IDisposable
{
    private readonly IdentityTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private async Task<Guid> SeedAsync(string email)
    {
        using var scope = _host.Services.CreateScope();
        var user = AdminUserMirror.Create(email, "Test Admin");
        await scope.ServiceProvider.GetRequiredService<IAdminUserMirrorRepository>().AddAsync(user);
        await scope.ServiceProvider.GetRequiredService<IIdentityUnitOfWork>().SaveChangesAsync();
        return user.Id;
    }

    private async Task<AdminUserMirror?> FindAsync(string email)
    {
        using var scope = _host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAdminUserMirrorRepository>().GetByEmailAsync(email);
    }

    [Fact]
    public async Task GetByEmailAsync_WhenUserExists_ReturnsUser()
    {
        var id = await SeedAsync("admin@example.test");

        var user = await FindAsync("admin@example.test");

        user!.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetByEmailAsync_WithMixedCaseEmail_MatchesCaseInsensitively()
    {
        var id = await SeedAsync("admin@example.test");

        var user = await FindAsync("ADMIN@Example.Test");

        user!.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetByEmailAsync_WhenUserMissing_ReturnsNull()
    {
        await SeedAsync("admin@example.test");

        var user = await FindAsync("missing@example.test");

        user.Should().BeNull();
    }

    [Fact]
    public async Task Update_AfterUntrackedRead_PersistsProfileChange()
    {
        await SeedAsync("admin@example.test");
        using (var scope = _host.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAdminUserMirrorRepository>();
            var tracked = await repo.GetByEmailAsync("admin@example.test");
            tracked!.UpdateProfile("Renamed Admin", "ar");
            repo.Update(tracked);
            await scope.ServiceProvider.GetRequiredService<IIdentityUnitOfWork>().SaveChangesAsync();
        }

        var user = await FindAsync("admin@example.test");

        user.Should().BeEquivalentTo(new { DisplayName = "Renamed Admin", Locale = "ar" });
    }
}
