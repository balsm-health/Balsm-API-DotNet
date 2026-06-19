using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Handlers;

public sealed class VerifyOtpHandler(
    AuthDbContext authDb,
    AccountDbContext accountDb,
    JwtService jwt) : IRequestHandler<VerifyOtpCommand, AuthTokenResult>
{
    public async Task<AuthTokenResult> Handle(VerifyOtpCommand cmd, CancellationToken ct)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();

        var identity = await authDb.UserIdentities
            .FirstOrDefaultAsync(i => i.Provider == "email" && i.EmailNormalized == email, ct);

        bool isNew = identity is null;
        Guid userId;

        if (isNew)
        {
            var account = UserAccount.Create(countryCode: "EG", preferredLanguage: "ar-EG");
            accountDb.UserAccounts.Add(account);
            await accountDb.SaveChangesAsync(ct);
            userId = account.Id;

            identity = UserIdentity.Create(userId, "email", email, email);
            identity.ConfirmEmail(DateTime.UtcNow);
            authDb.UserIdentities.Add(identity);
        }
        else
        {
            userId = identity!.UserId;
        }

        var accessToken = jwt.IssueAccessToken(userId, email);
        var (refreshRaw, refreshHash) = jwt.IssueRefreshToken();

        var refreshToken = UserRefreshToken.Create(userId, refreshHash, cmd.DeviceId, DateTime.UtcNow.AddDays(30));
        authDb.UserRefreshTokens.Add(refreshToken);
        await authDb.SaveChangesAsync(ct);
        await accountDb.SaveChangesAsync(ct);

        return new AuthTokenResult(accessToken, refreshRaw, userId, isNew);
    }
}
