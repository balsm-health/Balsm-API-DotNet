using MediatR;

namespace Balsm.Auth.Application.Commands;

/// <param name="AllowNewAccounts">False while <c>SocialSignIn:Enabled</c> is off: an existing
/// identity still signs in, an unknown subject gets <see cref="SocialSignInUnavailableException"/>.</param>
public sealed record ExchangeGoogleTokenCommand(
    string IdToken, Guid DeviceId, string DeviceLabel, string CountryCode,
    bool AllowNewAccounts = true) : IRequest<AuthTokenResult>;
