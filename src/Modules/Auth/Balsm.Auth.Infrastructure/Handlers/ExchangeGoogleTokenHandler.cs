using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Geofence.Domain;
using Balsm.Infrastructure.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Handlers;

public sealed class ExchangeGoogleTokenHandler(
    AuthDbContext authDb,
    AccountDbContext accountDb,
    GoogleOidcValidator googleOidc,
    IGeofenceService geofence,
    JwtService jwt) : IRequestHandler<ExchangeGoogleTokenCommand, AuthTokenResult>
{
    public async Task<AuthTokenResult> Handle(ExchangeGoogleTokenCommand cmd, CancellationToken ct)
    {
        if (await geofence.IsDeniedAsync(cmd.CountryCode, ct))
            throw new GeofenceDeniedException(cmd.CountryCode);

        var payload = await googleOidc.ValidateAsync(cmd.IdToken, ct)
            ?? throw new UnauthorizedAccessException("Google ID token validation failed");
        var email = (payload.Email ?? throw new InvalidOperationException("Google token missing email claim")).Trim().ToLowerInvariant();
        const string provider = "google";

        var identity = await authDb.UserIdentities
            .FirstOrDefaultAsync(i => i.Provider == provider && i.ProviderSubject == payload.Subject, ct);

        bool isNew = identity is null;
        Guid userId;

        if (isNew)
        {
            var account = UserAccount.Create(countryCode: cmd.CountryCode, preferredLanguage: "en");
            accountDb.UserAccounts.Add(account);
            await accountDb.SaveChangesAsync(ct);
            userId = account.Id;

            identity = UserIdentity.Create(userId, provider, payload.Subject, email);
            if (payload.EmailVerified) identity.ConfirmEmail(DateTime.UtcNow);
            authDb.UserIdentities.Add(identity);
        }
        else
        {
            userId = identity!.UserId;
        }

        var accessToken = jwt.IssueAccessToken(userId, email);
        var (refreshRaw, refreshHash) = jwt.IssueRefreshToken();
        authDb.UserRefreshTokens.Add(UserRefreshToken.Create(userId, refreshHash, cmd.DeviceId, DateTime.UtcNow.AddDays(30)));
        await authDb.SaveChangesAsync(ct);
        await accountDb.SaveChangesAsync(ct);

        return new AuthTokenResult(accessToken, refreshRaw, userId, isNew);
    }
}
