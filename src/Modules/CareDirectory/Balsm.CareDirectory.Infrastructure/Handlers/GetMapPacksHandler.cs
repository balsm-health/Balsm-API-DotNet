using Balsm.CareDirectory.Application.Queries;
using Balsm.CareDirectory.Infrastructure.MapPacks;
using MediatR;

namespace Balsm.CareDirectory.Infrastructure.Handlers;

/// <summary>
/// Serves the offline map-pack catalogue straight from the committed artifact.
///
/// No database, no filtering: the catalogue is identical for every caller,
/// which is exactly what makes it safe to cache and serve anonymously.
/// </summary>
public sealed class GetMapPacksHandler(MapPackCatalogue catalogue)
    : IRequestHandler<GetMapPacksQuery, IReadOnlyList<MapPackDto>>
{
    public Task<IReadOnlyList<MapPackDto>> Handle(GetMapPacksQuery request, CancellationToken cancellationToken)
        => Task.FromResult(catalogue.Packs);
}
