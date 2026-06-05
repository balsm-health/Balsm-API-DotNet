using Balsm.Entity.Application.DTOs;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Entity.Application.Queries;

public sealed record GetWorkspaceQuery : IRequest<Result<WorkspaceDto>>;
public sealed record GetEntityByIdQuery(Guid Id) : IRequest<Result<EntityDto>>;
public sealed record ListEntitiesQuery(Guid WorkspaceId, bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<EntityDto>>>;
public sealed record ListBranchesQuery(Guid EntityRootId, bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<BranchDto>>>;
public sealed record ListEntityTypesQuery : IRequest<Result<IReadOnlyList<EntityTypeDto>>>;
