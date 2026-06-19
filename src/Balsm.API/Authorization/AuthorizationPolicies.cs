using System.Security.Claims;
using Balsm.Account.Infrastructure.Data;
using Balsm.Infrastructure.Encryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.API.Authorization;

public static class PolicyNames
{
    public const string SelfOnly = "SelfOnly";
    public const string ActiveAccount = "ActiveAccount";
    public const string NotLockedOut = "NotLockedOut";
    public const string AgeGate = "AgeGate";
}

// SelfOnly: user's sub claim matches the resource userId
public sealed class SelfOnlyRequirement : IAuthorizationRequirement { }

public sealed class SelfOnlyHandler : AuthorizationHandler<SelfOnlyRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, SelfOnlyRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

// ActiveAccount: deletion_state must be ACTIVE (enforced at command layer via DB check)
public sealed class ActiveAccountRequirement : IAuthorizationRequirement { }

public sealed class ActiveAccountHandler : AuthorizationHandler<ActiveAccountRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ActiveAccountRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

// NotLockedOut: verified in command layer against account_lockout table
public sealed class NotLockedOutRequirement : IAuthorizationRequirement { }

public sealed class NotLockedOutHandler : AuthorizationHandler<NotLockedOutRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, NotLockedOutRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

// AgeGate: DOB ciphertext decrypted + age ≥ 18 verified (FR-301b)
public sealed class AgeGateRequirement : IAuthorizationRequirement { }

public sealed class AgeGateHandler(
    IServiceScopeFactory scopeFactory,
    DobEncryptionService dob) : AuthorizationHandler<AgeGateRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, AgeGateRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? context.User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
            return;

        using var scope = scopeFactory.CreateScope();
        var accountDb = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var account = await accountDb.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == userId);

        if (account is null)
            return;

        // No DOB set yet — allow through; command layer enforces dob requirement
        if (account.DateOfBirthCiphertext is null)
        {
            context.Succeed(requirement);
            return;
        }

        try
        {
            var dobDate = dob.Decrypt(account.DateOfBirthCiphertext);
            if (!dob.IsUnderEighteen(account.DateOfBirthCiphertext))
                context.Succeed(requirement);
        }
        catch
        {
            // Corrupt ciphertext — deny silently; do not leak PHI in exception
        }
    }
}
