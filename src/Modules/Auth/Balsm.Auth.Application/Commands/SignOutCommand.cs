using MediatR;

namespace Balsm.Auth.Application.Commands;

public sealed record SignOutCommand(Guid UserId, Guid DeviceId) : IRequest;
