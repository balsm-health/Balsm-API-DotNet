using MediatR;

namespace Balsm.EmergencyQr.Application.Commands;

public sealed record MintEmergencyQrCommand(Guid UserId, byte[] Ciphertext, string ProfileEtag, string PreferredLanguage, int TtlSeconds) : IRequest<MintEmergencyQrResult>;
public sealed record MintEmergencyQrResult(Guid TokenId, DateTime ExpiresAt);
