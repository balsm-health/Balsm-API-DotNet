using Balsm.Infrastructure.Platform;

namespace Balsm.Infrastructure.Audit;

public sealed class AuditLogWriter(PlatformDbContext db) : IAuditLogWriter
{
    public async Task WriteAsync(AuditLog row, CancellationToken ct = default)
    {
        db.AuditLogs.Add(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
