using System.Security.Cryptography;
using System.Text;
using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Balsm.Auth.Infrastructure.Handlers;

/// <summary>
/// Recovery claim flow (FR-046c/d/e). Support staff issues a time-bound HMAC token
/// via the support runbook; this handler validates it and issues fresh JWT pair.
/// Token format: HMAC-SHA256(email:timestamp_unix, Recovery:Secret) — valid 1 hour.
/// Quarantine of original identity (30d) is TODO: requires schema field.
/// </summary>
public sealed class RecoveryClaimHandler(
    AuthDbContext db,
    JwtService jwt,
    IConfiguration configuration,
    ILogger<RecoveryClaimHandler> logger) : IRequestHandler<RecoveryClaimCommand, RecoveryClaimResult>
{
    public async Task<RecoveryClaimResult> Handle(RecoveryClaimCommand cmd, CancellationToken ct)
    {
        var secret = configuration["Recovery:Secret"]
            ?? throw new InvalidOperationException("Recovery:Secret not configured");

        if (!ValidateSupportToken(cmd.Email, cmd.SupportToken, secret))
            throw new InvalidSupportTokenException();

        var email = cmd.Email.Trim().ToLowerInvariant();
        var identity = await db.UserIdentities
            .FirstOrDefaultAsync(i => i.Provider == "email" && i.EmailNormalized == email, ct)
            ?? throw new RecoveryIdentityNotFoundException();

        logger.LogInformation("Recovery claim issued for identity {Id}", identity.Id);

        var accessToken = jwt.IssueAccessToken(identity.UserId, email);
        var (refreshRaw, refreshHash) = jwt.IssueRefreshToken();
        db.UserRefreshTokens.Add(
            UserRefreshToken.Create(identity.UserId, refreshHash, cmd.DeviceId, DateTime.UtcNow.AddDays(30)));
        await db.SaveChangesAsync(ct);

        return new RecoveryClaimResult(accessToken, refreshRaw, identity.UserId);
    }

    private static bool ValidateSupportToken(string email, string token, string secret)
    {
        // Token format: "{unixTimestamp}:{hmac}" where hmac = HMAC-SHA256(email:unixTimestamp, secret)
        var parts = token.Split(':', 2);
        if (parts.Length != 2 || !long.TryParse(parts[0], out var unixTs))
            return false;

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(unixTs);
        if (DateTimeOffset.UtcNow - issuedAt > TimeSpan.FromHours(1))
            return false;

        var payload = $"{email.Trim().ToLowerInvariant()}:{unixTs}";
        var key = Encoding.UTF8.GetBytes(secret);
        var expected = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected.ToUpperInvariant()),
            Encoding.UTF8.GetBytes(parts[1].ToUpperInvariant()));
    }
}
