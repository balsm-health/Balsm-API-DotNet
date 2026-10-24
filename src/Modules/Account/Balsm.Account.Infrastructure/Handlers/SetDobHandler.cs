using Balsm.Account.Application.Commands;
using Balsm.Account.Infrastructure.Data;
using Balsm.Infrastructure.Encryption;
using MediatR;

namespace Balsm.Account.Infrastructure.Handlers;

public sealed class SetDobHandler(AccountDbContext db, DobEncryptionService dobEncryption)
    : IRequestHandler<SetDobCommand>
{
    public async Task Handle(SetDobCommand cmd, CancellationToken ct)
    {
        if (dobEncryption.IsUnderEighteen(dobEncryption.Encrypt(cmd.DateOfBirth)))
            throw new UnderEighteenException();

        var account = await db.UserAccounts.FindAsync([cmd.UserId], ct)
            ?? throw new InvalidOperationException("Account not found");

        account.SetDob(dobEncryption.Encrypt(cmd.DateOfBirth));
        await db.SaveChangesAsync(ct);
    }
}
