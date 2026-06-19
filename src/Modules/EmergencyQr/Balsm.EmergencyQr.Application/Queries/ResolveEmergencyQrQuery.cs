using MediatR;

namespace Balsm.EmergencyQr.Application.Queries;

public sealed record ResolveEmergencyQrQuery(Guid TokenId) : IRequest<ResolveEmergencyQrResult?>;
public sealed record ResolveEmergencyQrResult(byte[] Ciphertext, string PreferredLanguage, DateTime ExpiresAt);
