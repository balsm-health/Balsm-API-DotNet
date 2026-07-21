using Balsm.Account.Application.Commands;
using Balsm.Account.Infrastructure.Data;
using Balsm.Infrastructure.Encryption;
using MediatR;

namespace Balsm.Account.Infrastructure.Handlers;

public sealed class UpdateProfileHandler(AccountDbContext db, DobEncryptionService encryption)
    : IRequestHandler<UpdateProfileCommand>
{
    public async Task Handle(UpdateProfileCommand cmd, CancellationToken ct)
    {
        var account = await db.UserAccounts.FindAsync([cmd.UserId], ct)
            ?? throw new InvalidOperationException("Account not found");

        // Non-encrypted fields: null = unchanged, "" = clear.
        account.UpdateProfile(cmd.DisplayName, cmd.Bio, cmd.Gender, cmd.Nationality, cmd.Phone);

        // DOB is PHI — encrypt before storing, and keep the 18+ gate consistent
        // with SetDobHandler.
        if (cmd.DateOfBirth is { } dob)
        {
            var ciphertext = encryption.Encrypt(dob);
            if (encryption.IsUnderEighteen(ciphertext))
                throw new UnderEighteenException();
            account.SetDob(ciphertext);
        }

        // National ID is sensitive PII — null = unchanged, "" = clear,
        // otherwise store the AES-256-GCM ciphertext.
        if (cmd.NationalId is not null)
        {
            account.SetNationalId(
                cmd.NationalId.Length == 0 ? null : encryption.EncryptString(cmd.NationalId));
        }

        await db.SaveChangesAsync(ct);
    }
}
