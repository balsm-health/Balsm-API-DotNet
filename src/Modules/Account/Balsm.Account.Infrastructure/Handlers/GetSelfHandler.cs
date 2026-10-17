using Balsm.Account.Application.Queries;
using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Infrastructure.Encryption;
using MediatR;

namespace Balsm.Account.Infrastructure.Handlers;

public sealed class GetSelfHandler(
    AccountDbContext db,
    DobEncryptionService encryption) : IRequestHandler<GetSelfQuery, GetSelfResult>
{
    public async Task<GetSelfResult> Handle(GetSelfQuery query, CancellationToken ct)
    {
        var account = await db.UserAccounts.FindAsync([query.UserId], ct)
            ?? throw new InvalidOperationException("Account not found");

        int? dobYear = null;
        DateOnly? dob = null;
        string? nationalId = null;
        var decryptedPhi = false;

        if (account.DateOfBirthCiphertext is { Length: > 0 })
        {
            dob = encryption.Decrypt(account.DateOfBirthCiphertext);
            dobYear = dob.Value.Year;
            decryptedPhi = true;
        }

        if (account.NationalIdCiphertext is { Length: > 0 })
        {
            nationalId = encryption.DecryptString(account.NationalIdCiphertext);
            decryptedPhi = true;
        }

        // FR-048: every decrypt of a user's PHI is audit-logged. This is a
        // self-read, so actor == target. (Full correlation-id / source-IP
        // propagation from the request is a follow-up.)
        if (decryptedPhi)
        {
            db.UserAccountAuditLogs.Add(
                UserAccountAuditLog.Create(account.Id, account.Id, Guid.NewGuid(), sourceIp: null));
            await db.SaveChangesAsync(ct);
        }

        var firstName = account.FirstName;
        var lastName = account.LastName;
        if (firstName is null && lastName is null && !string.IsNullOrWhiteSpace(account.DisplayName))
        {
            var trimmed = account.DisplayName.Trim();
            var sp = trimmed.IndexOf(' ');
            firstName = sp < 0 ? trimmed : trimmed[..sp];
            lastName = sp < 0 ? null : trimmed[(sp + 1)..].Trim();
        }

        return new GetSelfResult(
            account.Id,
            firstName,
            lastName,
            account.Handle,
            account.DisplayName,
            account.Bio,
            account.Gender,
            account.Nationality,
            account.Phone,
            account.CountryCode,
            account.PreferredLanguage,
            account.DeletionState.ToString(),
            dobYear,
            dob,
            nationalId);
    }
}
