using MediatR;

namespace Balsm.Auth.Application.Commands;

/// <param name="AuthorizationCode">Apple's one-time code; exchanged for a refresh token kept for revocation.</param>
/// <param name="Nonce">The raw nonce; its SHA-256 hex must equal the ID token's <c>nonce</c> claim.</param>
public sealed record ExchangeAppleTokenCommand(
    string IdToken, Guid DeviceId, string DeviceLabel, string CountryCode,
    string? AuthorizationCode, string? Nonce) : IRequest<AuthTokenResult>;
