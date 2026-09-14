using MediatR;

namespace Balsm.EmergencyQr.Application.Commands;

/// <summary>
/// Replaces the encrypted snapshot of an active token in place (same jti), so a
/// permanent QR keeps resolving to current data. Owner-only.
/// </summary>
public sealed record UpdateEmergencyQrCiphertextCommand(
    Guid TokenId,
    Guid RequestingUserId,
    byte[] Ciphertext,
    string ProfileEtag,
    string PreferredLanguage) : IRequest;
