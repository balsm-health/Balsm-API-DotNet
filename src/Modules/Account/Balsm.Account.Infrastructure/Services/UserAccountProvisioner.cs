using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Account.Infrastructure.Services;

/// <summary>
/// Account-side implementation of the cross-module provisioning contract.
/// The only sanctioned path for another module (Auth) to cause an account
/// row to exist.
/// </summary>
public sealed class UserAccountProvisioner(AccountDbContext db) : IUserAccountProvisioner
{
    public async Task<Guid> ProvisionAsync(string countryCode, string preferredLanguage, CancellationToken ct = default)
    {
        var account = UserAccount.Create(countryCode: countryCode, preferredLanguage: preferredLanguage);
        db.UserAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account.Id;
    }

    public async Task<Guid> ProvisionWithPreferredIdAsync(
        string countryCode, string preferredLanguage, Guid preferredId, CancellationToken ct = default)
    {
        if (preferredId.Version != 7 || await db.UserAccounts.IgnoreQueryFilters().AnyAsync(a => a.Id == preferredId, ct))
            return await ProvisionAsync(countryCode, preferredLanguage, ct);

        var account = UserAccount.CreateWithId(preferredId, countryCode, preferredLanguage);
        db.UserAccounts.Add(account);
        try
        {
            await db.SaveChangesAsync(ct);
            return account.Id;
        }
        catch (DbUpdateException)
        {
            // Lost a race for the same id: fall back, never error.
            db.Entry(account).State = EntityState.Detached;
            return await ProvisionAsync(countryCode, preferredLanguage, ct);
        }
    }
}
