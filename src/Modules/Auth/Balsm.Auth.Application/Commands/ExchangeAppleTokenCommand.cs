using MediatR;

namespace Balsm.Auth.Application.Commands;

/// <param name="AuthorizationCode">Apple's one-time code; exchanged for a refresh token kept for revocation.</param>
/// <param name="Nonce">The raw nonce; its SHA-256 hex must equal the ID token's <c>nonce</c> claim.</param>
/// <param name="AllowNewAccounts">False while <c>SocialSignIn:Enabled</c> is off: an existing
/// identity still signs in, an unknown subject gets <see cref="SocialSignInUnavailableException"/>.</param>
public sealed record ExchangeAppleTokenCommand(
    string IdToken, Guid DeviceId, string DeviceLabel, string CountryCode,
    string? AuthorizationCode, string? Nonce, bool AllowNewAccounts = true) : IRequest<AuthTokenResult>;
