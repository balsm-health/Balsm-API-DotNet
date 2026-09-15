using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.SharedKernel.Contracts;

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
}
