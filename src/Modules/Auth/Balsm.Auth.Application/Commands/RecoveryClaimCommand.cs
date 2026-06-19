using MediatR;

namespace Balsm.Auth.Application.Commands;

public sealed record RecoveryClaimCommand(
    string Email,
    string SupportToken,
    Guid DeviceId,
    string DeviceLabel) : IRequest<RecoveryClaimResult>;

public sealed record RecoveryClaimResult(string AccessToken, string RefreshToken, Guid UserId);

public sealed class InvalidSupportTokenException() : Exception("Support token invalid or expired");
public sealed class RecoveryIdentityNotFoundException() : Exception("No identity found for recovery claim");
