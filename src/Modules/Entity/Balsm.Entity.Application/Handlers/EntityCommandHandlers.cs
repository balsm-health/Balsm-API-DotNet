using Balsm.Entity.Application.Commands;
using Balsm.Entity.Application.DTOs;
using Balsm.Entity.Domain;
using Balsm.Entity.Domain.Repositories;
using Balsm.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Balsm.Entity.Application.Handlers;

public sealed class CreateWorkspaceCommandHandler(
    IWorkspaceRepository repository,
    IEntityUnitOfWork uow,
    ILogger<CreateWorkspaceCommandHandler> logger)
    : IRequestHandler<CreateWorkspaceCommand, Result<WorkspaceDto>>
{
    public async Task<Result<WorkspaceDto>> Handle(CreateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        if (await repository.AnyAsync(cancellationToken).ConfigureAwait(false))
            return Result.Failure<WorkspaceDto>(new Error("Workspace.AlreadyExists", "A workspace already exists."));

        var workspace = Workspace.Create(request.Name, request.Slug, request.Locale);
        await repository.AddAsync(workspace, cancellationToken).ConfigureAwait(false);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Workspace {WorkspaceId} created", workspace.Id);
        return Result.Success(Map(workspace));
    }

    private static WorkspaceDto Map(Workspace w) =>
        new(w.Id, w.Name, w.Slug, w.Status.ToString(), w.LocaleDefault);
}

public sealed class UpdateWorkspaceCommandHandler(
    IWorkspaceRepository repository,
    IEntityUnitOfWork uow)
    : IRequestHandler<UpdateWorkspaceCommand, Result<WorkspaceDto>>
{
    public async Task<Result<WorkspaceDto>> Handle(UpdateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var workspace = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (workspace is null)
            return Result.Failure<WorkspaceDto>(Error.NotFound);

        workspace.Rename(request.Name);
        workspace.UpdateLocale(request.Locale);
        repository.Update(workspace);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new WorkspaceDto(workspace.Id, workspace.Name, workspace.Slug, workspace.Status.ToString(), workspace.LocaleDefault));
    }
}

public sealed class CreateEntityCommandHandler(
    IEntityRepository repository,
    IEntityTypeRepository typeRepository,
    IEntityUnitOfWork uow)
    : IRequestHandler<CreateEntityCommand, Result<EntityDto>>
{
    public async Task<Result<EntityDto>> Handle(CreateEntityCommand request, CancellationToken cancellationToken)
    {
        if (!await typeRepository.ExistsByCodeAsync(request.TypeCode, cancellationToken).ConfigureAwait(false))
            return Result.Failure<EntityDto>(new Error("EntityType.NotFound", "Entity type not found."));

        var entity = EntityRoot.Create(request.WorkspaceId, request.Name, request.TypeCode, request.RegistrationNumber);
        await repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(entity));
    }

    private static EntityDto Map(EntityRoot e) =>
        new(e.Id, e.WorkspaceId, e.Name, e.TypeCode, e.RegistrationNumber, e.IsDeleted);
}

public sealed class UpdateEntityCommandHandler(
    IEntityRepository repository,
    IEntityUnitOfWork uow)
    : IRequestHandler<UpdateEntityCommand, Result<EntityDto>>
{
    public async Task<Result<EntityDto>> Handle(UpdateEntityCommand request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return Result.Failure<EntityDto>(Error.NotFound);

        entity.Update(request.Name, request.RegistrationNumber);
        repository.Update(entity);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new EntityDto(entity.Id, entity.WorkspaceId, entity.Name, entity.TypeCode, entity.RegistrationNumber, entity.IsDeleted));
    }
}

public sealed class DeactivateEntityCommandHandler(
    IEntityRepository repository,
    IEntityUnitOfWork uow)
    : IRequestHandler<DeactivateEntityCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeactivateEntityCommand request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return Result.Failure<bool>(Error.NotFound);

        entity.Deactivate();
        repository.Update(entity);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }
}

public sealed class ReactivateEntityCommandHandler(
    IEntityUnitOfWork uow,
    IEntityRepository repository)
    : IRequestHandler<ReactivateEntityCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ReactivateEntityCommand request, CancellationToken cancellationToken)
    {
        var entity = await uow.GetEntityIncludingDeletedAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return Result.Failure<bool>(Error.NotFound);

        entity.Reactivate();
        repository.Update(entity);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }
}

public sealed class CreateBranchCommandHandler(
    IBranchRepository repository,
    IEntityRepository entityRepository,
    IEntityUnitOfWork uow)
    : IRequestHandler<CreateBranchCommand, Result<BranchDto>>
{
    public async Task<Result<BranchDto>> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        if (await entityRepository.GetByIdAsync(request.EntityRootId, cancellationToken).ConfigureAwait(false) is null)
            return Result.Failure<BranchDto>(new Error("Entity.NotFound", "Entity not found."));

        var branch = Branch.Create(request.EntityRootId, request.Name, request.AddressLine, request.City, request.Governorate, request.Phone);
        await repository.AddAsync(branch, cancellationToken).ConfigureAwait(false);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(branch));
    }

    private static BranchDto Map(Branch b) =>
        new(b.Id, b.EntityRootId, b.Name, b.AddressLine, b.City, b.Governorate, b.Phone, b.IsDeleted);
}

public sealed class UpdateBranchCommandHandler(
    IBranchRepository repository,
    IEntityUnitOfWork uow)
    : IRequestHandler<UpdateBranchCommand, Result<BranchDto>>
{
    public async Task<Result<BranchDto>> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (branch is null)
            return Result.Failure<BranchDto>(Error.NotFound);

        branch.Update(request.Name, request.AddressLine, request.City, request.Governorate, request.Phone);
        repository.Update(branch);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new BranchDto(branch.Id, branch.EntityRootId, branch.Name, branch.AddressLine, branch.City, branch.Governorate, branch.Phone, branch.IsDeleted));
    }
}

public sealed class DeactivateBranchCommandHandler(
    IBranchRepository repository,
    IEntityUnitOfWork uow)
    : IRequestHandler<DeactivateBranchCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeactivateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (branch is null)
            return Result.Failure<bool>(Error.NotFound);

        branch.Deactivate();
        repository.Update(branch);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }
}

public sealed class ReactivateBranchCommandHandler(
    IBranchRepository repository,
    IEntityUnitOfWork uow)
    : IRequestHandler<ReactivateBranchCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ReactivateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await uow.GetBranchIncludingDeletedAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (branch is null)
            return Result.Failure<bool>(Error.NotFound);

        branch.Reactivate();
        repository.Update(branch);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }
}
