using MediatR;

namespace Balsm.EmergencyQr.Application.Queries;

/// <param name="ClientClass">Coarse scanner class parsed from the User-Agent
/// ("web" / "app" / "unknown") — recorded in the owner's scan history; the raw
/// User-Agent never persists.</param>
public sealed record ResolveEmergencyQrQuery(Guid TokenId, string ClientClass = "unknown") : IRequest<ResolveEmergencyQrResult?>;
public sealed record ResolveEmergencyQrResult(byte[] Ciphertext, string Type, DateTime? ExpiresAt);
