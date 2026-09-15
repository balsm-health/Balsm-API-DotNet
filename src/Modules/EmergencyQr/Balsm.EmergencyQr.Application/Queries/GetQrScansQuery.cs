using MediatR;

namespace Balsm.EmergencyQr.Application.Queries;

/// <summary>Owner-only scan history (spec v2.0), newest first, capped.</summary>
public sealed record GetQrScansQuery(Guid UserId, int Limit = 50) : IRequest<IReadOnlyList<QrScanResult>>;
public sealed record QrScanResult(Guid TokenId, DateTime ResolvedAt, string ClientClass, string? Country);
