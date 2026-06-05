using Balsm.Entity.Application.Commands;
using Balsm.SharedKernel.Contracts;
using MediatR;

namespace Balsm.API.Services;

internal sealed class FirstRunOrchestrator(IMediator mediator) : IFirstRunOrchestrator
{
    public async Task SeedWorkspaceAsync(string name, string slug, string locale, CancellationToken ct = default)
    {
        await mediator.Send(new CreateWorkspaceCommand(name, slug, locale), ct).ConfigureAwait(false);
    }
}
