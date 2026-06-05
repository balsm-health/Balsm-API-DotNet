using System.Text.Json;
using Balsm.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Balsm.Infrastructure.Audit;

public sealed class AuditSaveChangesInterceptor(IAuditLogWriter writer) : SaveChangesInterceptor
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

        foreach (var entry in auditEntries)
        {
            await writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
