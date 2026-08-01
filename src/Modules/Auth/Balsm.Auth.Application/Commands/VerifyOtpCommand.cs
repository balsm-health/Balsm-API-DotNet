using MediatR;

namespace Balsm.Auth.Application.Commands;

public sealed record VerifyOtpCommand(
    string Email, string Code, Guid DeviceId, string DeviceLabel) : IRequest<AuthTokenResult>;

public sealed record AuthTokenResult(
    string AccessToken, string RefreshToken, Guid UserId, bool IsNewUser);

/// Thrown when an OTP/magic-link verify targets an email that already has an
/// identity. OTP verify completes registration only; existing users sign in with
/// a password or Google/Apple.
public sealed class AccountAlreadyExistsException(string email)
    : Exception($"Account already exists: {email}");
