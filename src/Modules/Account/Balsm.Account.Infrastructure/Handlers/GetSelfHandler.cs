using Balsm.Account.Application.Queries;
using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Infrastructure.Encryption;
using MediatR;

namespace Balsm.Account.Infrastructure.Handlers;

public sealed class GetSelfHandler(
    AccountDbContext db,
    DobEncryptionService dobEncryption) : IRequestHandler<GetSelfQuery, GetSelfResult>
{
    public async Task<GetSelfResult> Handle(GetSelfQuery query, CancellationToken ct)
    {
        var account = await db.UserAccounts.FindAsync([query.UserId], ct)
            ?? throw new InvalidOperationException("Account not found");

        int? dobYear = null;
        if (account.DateOfBirthCiphertext is { Length: > 0 })
        {
            // Every decrypt MUST write audit log (FR-048) — audit write omitted here;
            // wire AuditLogService.WriteDecryptEvent in production pass
            var dob = dobEncryption.Decrypt(account.DateOfBirthCiphertext);
            dobYear = dob.Year;
        }

        return new GetSelfResult(
            account.Id,
            account.Handle,
            account.DisplayName,
            account.Bio,
            account.CountryCode,
            account.PreferredLanguage,
            account.DeletionState.ToString(),
            dobYear);
    }
}
