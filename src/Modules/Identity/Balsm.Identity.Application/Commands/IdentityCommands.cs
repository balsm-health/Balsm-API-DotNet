using Balsm.Identity.Application.DTOs;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.Commands;

public sealed record UpdateCurrentAdminUserCommand(string Email, string DisplayName, string Locale)
    : IRequest<Result<AdminUserDto>>;
