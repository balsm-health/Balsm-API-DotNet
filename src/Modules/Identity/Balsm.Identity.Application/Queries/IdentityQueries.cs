using Balsm.Identity.Application.DTOs;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.Queries;

public sealed record GetCurrentAdminUserQuery(string Email) : IRequest<Result<AdminUserDto>>;
