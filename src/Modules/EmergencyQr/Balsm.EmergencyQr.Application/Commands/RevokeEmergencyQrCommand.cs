using MediatR;

namespace Balsm.EmergencyQr.Application.Commands;

public sealed record RevokeEmergencyQrCommand(Guid TokenId, Guid RequestingUserId) : IRequest;
