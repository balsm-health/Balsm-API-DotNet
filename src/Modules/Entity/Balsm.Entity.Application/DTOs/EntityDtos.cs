namespace Balsm.Entity.Application.DTOs;

public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string LocaleDefault);

public sealed record EntityTypeDto(
    Guid Id,
    string Code,
    string LabelEn,
    string LabelAr,
    bool IsSeeded);

public sealed record EntityDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string TypeCode,
    string? RegistrationNumber,
    bool IsDeleted);

public sealed record BranchDto(
    Guid Id,
    Guid EntityRootId,
    string Name,
    string? AddressLine,
    string? City,
    string? Governorate,
    string? Phone,
    bool IsDeleted);
