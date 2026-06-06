using System.Text.Json;
using Balsm.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Infrastructure.Audit;

// The writer is resolved lazily (not constructor-injected) to avoid a DI cycle:
// this interceptor is registered into PlatformDbContext's options, while
// IAuditLogWriter depends on PlatformDbContext — eager injection deadlocks
// DbContext construction.
public sealed class AuditSaveChangesInterceptor(IServiceProvider serviceProvider) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return result;

        var ctx = AuditContext.Current;
        var now = DateTime.UtcNow;

        var auditEntries = eventData.Context.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e =>
            {
                var action = e.State switch
                {
                    
                    EntityState.Added => "Created",
                    EntityState.Deleted => "Deleted",
                    _ => e.Entity.IsDeleted ? "SoftDeleted" : "Updated"
                };

                return new AuditLog
                {
                    OccurredAt = now,
                    Actor = ctx?.Actor,
                    SourceIp = ctx?.SourceIp,
                    CorrelationId = ctx?.CorrelationId,
                    Module = e.Entity.GetType().Namespace?.Split('.').LastOrDefault() ?? "Unknown",
                    Action = action,
                    TargetType = e.Entity.GetType().Name,
                    TargetId = e.Entity.Id.ToString(),
                    DetailsJson = JsonSerializer.Serialize(new
                    {
                        entityState = e.State.ToString()
                    }),
                    CreatedAt = now
                };
            }).ToList();

        if (auditEntries.Count == 0)
            return result;

        var writer = serviceProvider.GetRequiredService<IAuditLogWriter>();
        foreach (var entry in auditEntries)
        {
            await writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
