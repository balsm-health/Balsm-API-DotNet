using MediatR;

namespace Balsm.Auth.Application.Commands;

/// Exchanges a raw magic sign-in link token for a session. Mirrors
/// <see cref="VerifyOtpCommand"/> but selects the challenge by link-token hash
/// instead of the 6-digit code.
public sealed record VerifyLinkCommand(
    string Token, Guid DeviceId, string DeviceLabel) : IRequest<AuthTokenResult>;
