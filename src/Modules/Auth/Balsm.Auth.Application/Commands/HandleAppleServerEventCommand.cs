using Balsm.Infrastructure.Auth;
using MediatR;

namespace Balsm.Auth.Application.Commands;

/// <summary>
/// Applies one validated Apple server-to-server event to the matching Apple
/// identity. An unknown subject or event type is a no-op; the Balsm account is
/// never deleted by an event.
/// </summary>
public sealed record HandleAppleServerEventCommand(AppleServerEvent Event) : IRequest;
