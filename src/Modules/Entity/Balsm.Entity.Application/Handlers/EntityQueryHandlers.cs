using Balsm.Entity.Application.DTOs;
using Balsm.Entity.Application.Queries;
using Balsm.Entity.Domain.Repositories;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Entity.Application.Handlers;

public sealed class GetWorkspaceQueryHandler(IWorkspaceRepository repository)
    : IRequestHandler<GetWorkspaceQuery, Result<WorkspaceDto>>
{
    public async Task<Result<WorkspaceDto>> Handle(GetWorkspaceQuery request, CancellationToken cancellationToken)
    {
        var ws = await repository.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (ws is null)
            return Result.Failure<WorkspaceDto>(Error.NotFound);
        return Result.Success(new WorkspaceDto(ws.Id, ws.Name, ws.Slug, ws.Status.ToString(), ws.LocaleDefault));
    }
}

public sealed class GetEntityByIdQueryHandler(IEntityRepository repository)
    : IRequestHandler<GetEntityByIdQuery, Result<EntityDto>>
{
    public async Task<Result<EntityDto>> Handle(GetEntityByIdQuery request, CancellationToken cancellationToken)
    {
        var e = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (e is null)
            return Result.Failure<EntityDto>(Error.NotFound);
        return Result.Success(new EntityDto(e.Id, e.WorkspaceId, e.Name, e.TypeCode, e.RegistrationNumber, e.IsDeleted));
    }
}

public sealed class ListEntitiesQueryHandler(IEntityRepository repository)
    : IRequestHandler<ListEntitiesQuery, Result<IReadOnlyList<EntityDto>>>
{
    public async Task<Result<IReadOnlyList<EntityDto>>> Handle(ListEntitiesQuery request, CancellationToken cancellationToken)
    {
        var list = await repository.ListAsync(request.WorkspaceId, request.IncludeInactive, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<EntityDto>>(list.Select(e => new EntityDto(e.Id, e.WorkspaceId, e.Name, e.TypeCode, e.RegistrationNumber, e.IsDeleted)).ToList());
    }
}

public sealed class ListBranchesQueryHandler(IBranchRepository repository)
    : IRequestHandler<ListBranchesQuery, Result<IReadOnlyList<BranchDto>>>
{
    public async Task<Result<IReadOnlyList<BranchDto>>> Handle(ListBranchesQuery request, CancellationToken cancellationToken)
    {
        var list = await repository.ListByEntityAsync(request.EntityRootId, request.IncludeInactive, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<BranchDto>>(list.Select(b => new BranchDto(b.Id, b.EntityRootId, b.Name, b.AddressLine, b.City, b.Governorate, b.Phone, b.IsDeleted)).ToList());
    }
}

public sealed class ListEntityTypesQueryHandler(IEntityTypeRepository repository)
    : IRequestHandler<ListEntityTypesQuery, Result<IReadOnlyList<EntityTypeDto>>>
{
    public async Task<Result<IReadOnlyList<EntityTypeDto>>> Handle(ListEntityTypesQuery request, CancellationToken cancellationToken)
    {
        var list = await repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<EntityTypeDto>>(list.Select(t => new EntityTypeDto(t.Id, t.Code, t.LabelEn, t.LabelAr, t.IsSeeded)).ToList());
    }
}
