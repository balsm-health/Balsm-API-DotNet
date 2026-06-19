using Balsm.Account.Application.Commands;
using Balsm.Account.Infrastructure.Data;
using MediatR;

namespace Balsm.Account.Infrastructure.Handlers;

public sealed class ChangeLanguageHandler(AccountDbContext db) : IRequestHandler<ChangeLanguageCommand>
{
    public async Task Handle(ChangeLanguageCommand cmd, CancellationToken ct)
    {
        var account = await db.UserAccounts.FindAsync([cmd.UserId], ct)
            ?? throw new InvalidOperationException("Account not found");
        account.ChangeLanguage(cmd.Language);
        await db.SaveChangesAsync(ct);
    }
}
