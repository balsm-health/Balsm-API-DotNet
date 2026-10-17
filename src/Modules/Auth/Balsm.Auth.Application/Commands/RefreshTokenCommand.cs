using MediatR;

namespace Balsm.Auth.Application.Commands;

public sealed record RefreshTokenCommand(string RefreshToken, Guid DeviceId) : IRequest<RefreshTokenResult>;
public sealed record RefreshTokenResult(string AccessToken, string RefreshToken);
