using Balsm.Auth.Application.Commands;
using Balsm.Auth.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Balsm.Auth.Infrastructure.Handlers;

/// Consent revoked or Apple account deleted: drop the Apple refresh token and
/// sign the user out everywhere. The Balsm account and on-device data stay —
/// deleting a health account is the user's own action. Hide-My-Email toggles
/// refresh the email snapshot. Logs carry UserId and Type, never sub or email.
public sealed class HandleAppleServerEventHandler(
    AuthDbContext db,
    ILogger<HandleAppleServerEventHandler> logger) : IRequestHandler<HandleAppleServerEventCommand>
{
    private const string Provider = "apple";

    public async Task Handle(HandleAppleServerEventCommand cmd, CancellationToken ct)
    {
        var evt = cmd.Event;
        var identity = await db.UserIdentities
            .FirstOrDefaultAsync(i => i.Provider == Provider && i.ProviderSubject == evt.Subject, ct)
            .ConfigureAwait(false);
        if (identity is null)
        {
            logger.LogInformation("Apple server event {EventType} for no known identity", evt.Type);
            return;
        }

        switch (evt.Type)
        {
            case "consent-revoked":
            case "account-delete":
                identity.ClearAppleRefreshToken();
                var now = DateTime.UtcNow;
                var sessions = await db.UserRefreshTokens
                    .Where(t => t.UserId == identity.UserId && t.RevokedAt == null && t.ExpiresAt > now)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                foreach (var session in sessions) session.Revoke();
                break;
            case "email-disabled":
            case "email-enabled":
                identity.RefreshProviderEmail(evt.Email);
                break;
            default:
                logger.LogInformation("Apple server event {EventType} ignored for user {UserId}", evt.Type, identity.UserId);
                return;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Apple server event {EventType} applied for user {UserId}", evt.Type, identity.UserId);
    }
}
