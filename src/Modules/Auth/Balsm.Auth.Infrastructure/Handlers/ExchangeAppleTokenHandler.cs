using System.Security.Cryptography;
using System.Text;
using Balsm.Auth.Application.Commands;
using Balsm.SharedKernel.Contracts;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Geofence.Domain;
using Balsm.Infrastructure.Auth;
using Balsm.Infrastructure.Encryption;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Balsm.Auth.Infrastructure.Handlers;

public sealed class ExchangeAppleTokenHandler(
    AuthDbContext authDb,
    IUserAccountProvisioner accountProvisioner,
    AppleOidcValidator appleOidc,
    AppleTokenClient appleTokens,
    AppleRefreshTokenCipher cipher,
    IGeofenceService geofence,
    JwtService jwt,
    ILogger<ExchangeAppleTokenHandler> logger) : IRequestHandler<ExchangeAppleTokenCommand, AuthTokenResult>
{
    public async Task<AuthTokenResult> Handle(ExchangeAppleTokenCommand cmd, CancellationToken ct)
    {
        if (await geofence.IsDeniedAsync(cmd.CountryCode, ct))
            throw new GeofenceDeniedException(cmd.CountryCode);

        var payload = await appleOidc.ValidateAsync(cmd.IdToken, ct)
            ?? throw new UnauthorizedAccessException("Apple ID token validation failed");
        if (!NonceMatches(cmd.Nonce, payload.Nonce))
            throw new UnauthorizedAccessException("Apple ID token nonce mismatch");

        // Keyed on `sub`. Apple sends the email only sometimes (reliably on the
        // first sign-in), so a token without one leaves the stored snapshot.
        var email = string.IsNullOrWhiteSpace(payload.Email) ? null : payload.Email.Trim().ToLowerInvariant();
        const string provider = "apple";

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

        // A sign-in is a fresh consent: Apple revoke events older than this are stale.
        identity.MarkAppleConsent(DateTime.UtcNow);
        await CaptureAppleRefreshTokenAsync(identity, cmd.AuthorizationCode, ct);

        var accessToken = jwt.IssueAccessToken(userId, identity.EmailNormalized ?? string.Empty);
        var (refreshRaw, refreshHash) = jwt.IssueRefreshToken();
        authDb.UserRefreshTokens.Add(UserRefreshToken.Create(userId, refreshHash, cmd.DeviceId, DateTime.UtcNow.AddDays(30)));
        await authDb.SaveChangesAsync(ct);

        return new AuthTokenResult(accessToken, refreshRaw, userId, isNew);
    }

    // The app gives Apple sha256(raw nonce) and sends us the raw value; Apple
    // echoes the hash in the token, binding the token to this request.
    private static bool NonceMatches(string? rawNonce, string? tokenNonce)
    {
        if (string.IsNullOrWhiteSpace(rawNonce) || tokenNonce is null) return false;
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawNonce))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(tokenNonce));
    }

    // Best effort: the token only serves revocation at account deletion, so
    // any failure here is logged (user id only) and sign-in carries on.
    private async Task CaptureAppleRefreshTokenAsync(UserIdentity identity, string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || !appleTokens.IsConfigured) return;
        try
        {
            var refreshToken = await appleTokens.ExchangeCodeAsync(code, ct);
            if (refreshToken is null)
            {
                logger.LogWarning("Apple refresh token not obtained for user {UserId}", identity.UserId);
                return;
            }
            identity.SetAppleRefreshToken(cipher.Encrypt(refreshToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Apple refresh token capture failed for user {UserId}: {ExceptionType}",
                identity.UserId, ex.GetType().Name);
        }
    }
}
