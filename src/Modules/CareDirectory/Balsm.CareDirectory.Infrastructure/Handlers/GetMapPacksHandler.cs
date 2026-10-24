using Balsm.CareDirectory.Application.Queries;
using Balsm.CareDirectory.Domain.Entities;
using Balsm.CareDirectory.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.CareDirectory.Infrastructure.Handlers;

/// <summary>
/// Serves the offline catalogue from the table the nightly job writes.
///
/// A plain query, deliberately: the alternatives were a committed file, which
/// would need an API redeploy every night once places rebuild nightly, and
/// reading object storage per request, which puts the CDN on the user's
/// request path and can advertise a version that is not actually there.
/// </summary>
public sealed class GetMapPacksHandler(CareDirectoryDbContext db)
    : IRequestHandler<GetMapPacksQuery, IReadOnlyList<MapPackDto>>
{
    public async Task<IReadOnlyList<MapPackDto>> Handle(GetMapPacksQuery request, CancellationToken cancellationToken)
    {
        var arabic = string.Equals(request.Lang, "ar", StringComparison.OrdinalIgnoreCase);

        var rows = await db.MapPackArtifacts
            .AsNoTracking()
            .OrderBy(a => a.GovernorateId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var packs = new List<MapPackDto>();
        foreach (var group in rows.GroupBy(a => a.GovernorateId))
        {
            var basemap = group.FirstOrDefault(a => a.Kind == MapPackArtifactKind.Basemap);
            var places = group.FirstOrDefault(a => a.Kind == MapPackArtifactKind.Places);

            // Both or neither. A governorate mid-rollout — basemap uploaded,
            // tonight's places not yet — would otherwise be offered as a
            // download that cannot show a single pharmacy.
            if (basemap is null || places is null) continue;

            packs.Add(new MapPackDto(
                Id: group.Key,
                Name: arabic ? basemap.NameAr : basemap.NameEn,
                Bounds: [basemap.West, basemap.South, basemap.East, basemap.North],
                Basemap: Project(basemap),
                Places: Project(places)));
        }

        return packs;
    }

    private static MapPackArtifactDto Project(MapPackArtifact a) =>
        new(a.Version, a.SizeBytes, a.Sha256, a.Url, a.PlaceCount);
}
