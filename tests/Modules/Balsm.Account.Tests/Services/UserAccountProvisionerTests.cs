using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace Balsm.Account.Tests.Services;

public sealed class UserAccountProvisionerTests : IDisposable
{
    private static readonly Guid PreferredV7Id = Guid.Parse("01900000-0000-7000-8000-000000000001");
    private static readonly Guid PreferredV4Id = Guid.Parse("00000000-0000-4000-8000-000000000001");

    private readonly AccountTestDatabase _database = new();

    [Fact]
    public async Task ProvisionAsync_Always_PersistsAccountWithCountryAndLanguage()
    {
        var provisioner = new UserAccountProvisioner(_database.Db);

        var id = await provisioner.ProvisionAsync("SA", "ar");

        using var verify = _database.NewContext();
        var account = await verify.UserAccounts.FindAsync(id);
        account.Should().BeEquivalentTo(new { CountryCode = "SA", PreferredLanguage = "ar" });
    }

    [Fact]
    public async Task ProvisionWithPreferredIdAsync_FreeV7Id_UsesPreferredId()
    {
        var provisioner = new UserAccountProvisioner(_database.Db);

        var id = await provisioner.ProvisionWithPreferredIdAsync("SA", "en", PreferredV7Id);

        id.Should().Be(PreferredV7Id);
    }

    [Fact]
    public async Task ProvisionWithPreferredIdAsync_NonV7Id_IgnoresPreferredId()
    {
        var provisioner = new UserAccountProvisioner(_database.Db);

        var id = await provisioner.ProvisionWithPreferredIdAsync("SA", "en", PreferredV4Id);

        id.Should().NotBe(PreferredV4Id);
    }

    [Fact]
    public async Task ProvisionWithPreferredIdAsync_TakenV7Id_ProvisionsFreshId()
    {
        _database.Db.UserAccounts.Add(UserAccount.CreateWithId(PreferredV7Id, "SA", "en"));
        await _database.Db.SaveChangesAsync();
        var provisioner = new UserAccountProvisioner(_database.Db);

        var id = await provisioner.ProvisionWithPreferredIdAsync("SA", "en", PreferredV7Id);

        id.Should().NotBe(PreferredV7Id);
    }

    public void Dispose() => _database.Dispose();
}
