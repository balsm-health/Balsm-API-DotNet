using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Account.Infrastructure.Services;

/// <summary>
/// Account-side implementation of the display-name contract: @handle and
/// display name only — never date of birth, national id, phone or any other
/// profile field. An account in its deletion grace period is treated as gone and is
/// absent, like one already purged.
/// </summary>
public sealed class AccountDisplayNameReader(AccountDbContext db) : IAccountDisplayNameReader
{
    public async Task<IReadOnlyDictionary<Guid, AccountDisplayName>> GetAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, AccountDisplayName>();

        var ids = userIds.Distinct().ToList();
        var rows = await db.UserAccounts.AsNoTracking()
            .Where(a => ids.Contains(a.Id) && a.DeletionState != DeletionState.DELETION_REQUESTED)
            .Select(a => new { a.Id, a.Handle, a.DisplayName, a.FirstName, a.LastName })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows.ToDictionary(
            r => r.Id,
            r => new AccountDisplayName(r.Handle, DisplayNameOf(r.DisplayName, r.FirstName, r.LastName)));
    }

    private static string? DisplayNameOf(string? displayName, string? firstName, string? lastName)
    {
        if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
        var composed = $"{firstName} {lastName}".Trim();
        return composed.Length == 0 ? null : composed;
    }
}
