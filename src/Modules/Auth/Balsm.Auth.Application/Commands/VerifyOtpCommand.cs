using MediatR;

namespace Balsm.Auth.Application.Commands;

public sealed record VerifyOtpCommand(
    string Email, string Code, Guid DeviceId, string DeviceLabel,
    Guid? ClientAccountId = null) : IRequest<AuthTokenResult>;

public sealed record AuthTokenResult(
    string AccessToken, string RefreshToken, Guid UserId, bool IsNewUser,
    bool AdoptedClientId = false);
