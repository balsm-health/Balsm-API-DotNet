using MediatR;

namespace Balsm.CareDirectory.Application.Queries;

/// <summary>
/// One place by id — what a tapped map pin needs.
///
/// The map ships pins without names or contact details, so this is where the
/// detail sheet gets them. [Lat]/[Lng] are the viewer's position, used only to
/// compute distance; omit them and distance comes back as 0.
/// </summary>
public sealed record GetCarePlaceQuery(Guid Id, double? Lat, double? Lng) : IRequest<CareEntityDto?>;
