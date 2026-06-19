using MediatR;

namespace Balsm.EmergencyQr.Application.Queries;

public sealed record GetActiveQrQuery(Guid UserId) : IRequest<GetActiveQrResult?>;
public sealed record GetActiveQrResult(Guid TokenId, DateTime ExpiresAt, int TtlSeconds);
