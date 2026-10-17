using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Handlers;

/// Caps how often a sign-in secret can be guessed, for every path that checks
/// one: code verify, password reset and password sign-in.
///
/// Two limits. Each emailed code allows <see cref="OtpChallenge.MaxAttempts"/>
/// tries, counted in the database before the comparison so parallel guesses
/// cannot share a try. And <see cref="AccountLockout"/> locks the address after
/// five failures in ten minutes, whichever secret was guessed. Failures count
/// for addresses with no account too: locking only real ones would tell a
/// caller which addresses exist.
internal static class CredentialAttempts
{
    private const string EmailIdentifier = "email";

    /// When the address is locked, until when; otherwise null.
    public static async Task<DateTime?> LockedUntilAsync(AuthDbContext db, string email, CancellationToken ct)
    {
        var lockout = await FindAsync(db, email, ct);
        return lockout?.IsLocked == true ? lockout.LockedUntil : null;
    }

    /// Throws <see cref="AccountLockedException"/> while the address is locked.
    public static async Task EnsureNotLockedAsync(AuthDbContext db, string email, CancellationToken ct)
    {
        if (await LockedUntilAsync(db, email, ct) is { } until) throw new AccountLockedException(until);
    }

    /// Redeems the newest live code for <paramref name="email"/> when
    /// <paramref name="matches"/> accepts it, and returns that challenge
    /// (consumed, not yet saved). Null when there is no live code, its tries
    /// are spent, or it does not match; the failure is already recorded.
    public static async Task<OtpChallenge?> RedeemCodeAsync(
        AuthDbContext db, string email, Func<OtpChallenge, bool> matches, CancellationToken ct)
    {
        await EnsureNotLockedAsync(db, email, ct);

        var challenge = await db.OtpChallenges
            .Where(c => c.EmailNormalized == email && c.ConsumedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        // Take a try before comparing. The conditional update is the counter:
        // when it changes no row, the tries are gone, however many requests
        // arrived at once.
        var tookATry = challenge is not null && challenge.IsRedeemable &&
            await db.OtpChallenges
                .Where(c => c.Id == challenge.Id && c.ConsumedAt == null && c.Attempts < OtpChallenge.MaxAttempts)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Attempts, c => c.Attempts + 1), ct) == 1;

        if (tookATry && matches(challenge!))
        {
            challenge!.Consume();
            await RecordSuccessAsync(db, email, ct);
            return challenge;
        }

        await RecordFailureAsync(db, email, ct);
        return null;
    }

    /// Counts one failed attempt against the address and saves it.
    public static async Task RecordFailureAsync(AuthDbContext db, string email, CancellationToken ct)
    {
        var lockout = await FindAsync(db, email, ct);
        if (lockout is null)
        {
            lockout = AccountLockout.Create(email, EmailIdentifier);
            db.AccountLockouts.Add(lockout);
        }
        lockout.RecordFailure();
        await db.SaveChangesAsync(ct);
    }

    /// Clears past failures after a success. The caller saves.
    public static async Task RecordSuccessAsync(AuthDbContext db, string email, CancellationToken ct) =>
        (await FindAsync(db, email, ct))?.RecordSuccess();

    private static Task<AccountLockout?> FindAsync(AuthDbContext db, string email, CancellationToken ct) =>
        db.AccountLockouts.FirstOrDefaultAsync(l => l.Identifier == email && l.IdentifierType == EmailIdentifier, ct);
}
