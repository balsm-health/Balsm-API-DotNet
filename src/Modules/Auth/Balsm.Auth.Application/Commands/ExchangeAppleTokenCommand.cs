using MediatR;

namespace Balsm.Auth.Application.Commands;

public sealed record ExchangeAppleTokenCommand(
    string IdToken, Guid DeviceId, string DeviceLabel, string CountryCode) : IRequest<AuthTokenResult>;
