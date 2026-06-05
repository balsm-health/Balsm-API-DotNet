using Balsm.Identity.Application.Commands;
using Balsm.Identity.Application.DTOs;
using Balsm.Identity.Application.Queries;
using Balsm.Identity.Domain;
using Balsm.Identity.Domain.Repositories;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.Handlers;

internal sealed class GetCurrentAdminUserQueryHandler(IAdminUserMirrorRepository repo)
    : IRequestHandler<GetCurrentAdminUserQuery, Result<AdminUserDto>>
{
    public async Task<Result<AdminUserDto>> Handle(GetCurrentAdminUserQuery request, CancellationToken ct)
    {
        var user = await repo.GetByEmailAsync(request.Email, ct).ConfigureAwait(false);
        if (user is null)
            return Result.Failure<AdminUserDto>(new Error("Identity.NotFound", "Admin user not found."));

        return Result.Success(ToDto(user));
    }

    private static AdminUserDto ToDto(AdminUserMirror u) =>
        new(u.Id, u.Email, u.DisplayName, u.Role, u.Locale, u.LastLoginAt, u.PasswordChangedAt);
}

internal sealed class UpdateCurrentAdminUserCommandHandler(
    IAdminUserMirrorRepository repo,
    IIdentityUnitOfWork uow)
    : IRequestHandler<UpdateCurrentAdminUserCommand, Result<AdminUserDto>>
{
    public async Task<Result<AdminUserDto>> Handle(UpdateCurrentAdminUserCommand request, CancellationToken ct)
    {
        var user = await repo.GetByEmailAsync(request.Email, ct).ConfigureAwait(false);
        if (user is null)
            return Result.Failure<AdminUserDto>(new Error("Identity.NotFound", "Admin user not found."));

        user.UpdateProfile(request.DisplayName, request.Locale);
        repo.Update(user);
        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success(new AdminUserDto(
            user.Id, user.Email, user.DisplayName, user.Role, user.Locale,
            user.LastLoginAt, user.PasswordChangedAt));
    }
}
