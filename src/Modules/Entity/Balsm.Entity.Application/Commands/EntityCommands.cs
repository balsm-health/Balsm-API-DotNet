using Balsm.Entity.Application.DTOs;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Entity.Application.Commands;

public sealed record CreateWorkspaceCommand(string Name, string Slug, string Locale = "en")
    : IRequest<Result<WorkspaceDto>>;

public sealed record UpdateWorkspaceCommand(Guid Id, string Name, string Locale)
    : IRequest<Result<WorkspaceDto>>;

public sealed record CreateEntityCommand(Guid WorkspaceId, string Name, string TypeCode, string? RegistrationNumber = null)
    : IRequest<Result<EntityDto>>;

public sealed record UpdateEntityCommand(Guid Id, string Name, string? RegistrationNumber)
    : IRequest<Result<EntityDto>>;

public sealed record DeactivateEntityCommand(Guid Id)
    : IRequest<Result<bool>>;

public sealed record ReactivateEntityCommand(Guid Id)
    : IRequest<Result<bool>>;

public sealed record CreateBranchCommand(
    Guid EntityRootId,
    string Name,
    string? AddressLine = null,
    string? City = null,
    string? Governorate = null,
    string? Phone = null)
    : IRequest<Result<BranchDto>>;

public sealed record UpdateBranchCommand(
    Guid Id,
    string Name,
    string? AddressLine,
    string? City,
    string? Governorate,
    string? Phone)
    : IRequest<Result<BranchDto>>;

public sealed record DeactivateBranchCommand(Guid Id)
    : IRequest<Result<bool>>;

public sealed record ReactivateBranchCommand(Guid Id)
    : IRequest<Result<bool>>;
