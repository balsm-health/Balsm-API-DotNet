using Balsm.Auth.Application.Commands;
using Balsm.SharedKernel.Contracts;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Geofence.Domain;
using Balsm.Infrastructure.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Handlers;

public sealed class ExchangeGoogleTokenHandler(
    AuthDbContext authDb,
    IUserAccountProvisioner accountProvisioner,
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
        // The account is keyed on `sub`; the email is a snapshot that may be
        // absent and may change at Google. It never links to another identity.
        var email = string.IsNullOrWhiteSpace(payload.Email) ? null : payload.Email.Trim().ToLowerInvariant();
        const string provider = "google";

        var identity = await authDb.UserIdentities
            .FirstOrDefaultAsync(i => i.Provider == provider && i.ProviderSubject == payload.Subject, ct);

        bool isNew = identity is null;
        Guid userId;

        if (isNew)
        {
            var provisionedId = await accountProvisioner.ProvisionAsync(cmd.CountryCode, "en", ct);
            userId = provisionedId;

            identity = UserIdentity.Create(userId, provider, payload.Subject, email);
            if (payload.EmailVerified && email is not null) identity.ConfirmEmail(DateTime.UtcNow);
            authDb.UserIdentities.Add(identity);
        }
        else
        {
            userId = identity!.UserId;
            identity.RefreshProviderEmail(email);
            if (payload.EmailVerified && email is not null && identity.EmailConfirmedAt is null)
                identity.ConfirmEmail(DateTime.UtcNow);
        }

        var accessToken = jwt.IssueAccessToken(userId, identity.EmailNormalized ?? string.Empty);
        var (refreshRaw, refreshHash) = jwt.IssueRefreshToken();
        authDb.UserRefreshTokens.Add(UserRefreshToken.Create(userId, refreshHash, cmd.DeviceId, DateTime.UtcNow.AddDays(30)));
        await authDb.SaveChangesAsync(ct);

        return new AuthTokenResult(accessToken, refreshRaw, userId, isNew);
    }
}
