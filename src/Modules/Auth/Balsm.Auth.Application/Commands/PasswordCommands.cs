using MediatR;

namespace Balsm.Auth.Application.Commands;

/// Set or change the signed-in user's email-identity password.
public sealed record SetPasswordCommand(Guid UserId, string Password) : IRequest;

/// Sign in with email + password (returning users who have set a password).
public sealed record PasswordSignInCommand(
    string Email, string Password, Guid DeviceId, string DeviceLabel)
    : IRequest<AuthTokenResult>;

/// Reset a forgotten password using the emailed OTP code as the reset token.
public sealed record ResetPasswordCommand(
    string Email, string Code, string NewPassword) : IRequest;
