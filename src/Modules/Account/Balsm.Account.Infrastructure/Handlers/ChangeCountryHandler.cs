using Balsm.Account.Application.Commands;
using Balsm.Account.Infrastructure.Data;
using MediatR;

namespace Balsm.Account.Infrastructure.Handlers;

public sealed class ChangeCountryHandler(AccountDbContext db) : IRequestHandler<ChangeCountryCommand>
{
    public async Task Handle(ChangeCountryCommand cmd, CancellationToken ct)
    {
        var account = await db.UserAccounts.FindAsync([cmd.UserId], ct)
            ?? throw new InvalidOperationException("Account not found");
        // RR-001: DOB ciphertext not migrated on country change (compliance gap, documented)
        account.ChangeCountry(cmd.CountryCode);
        await db.SaveChangesAsync(ct);
    }
}
