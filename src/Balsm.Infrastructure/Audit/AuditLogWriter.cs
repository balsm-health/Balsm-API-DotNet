using Balsm.Infrastructure.Platform;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Infrastructure.Audit;

public sealed class AuditLogWriter(PlatformDbContext db) : IAuditLogWriter
{
    public async Task WriteAsync(AuditLog row, CancellationToken ct = default)
    {
        // Postgres generates Sequence (identity column). The SQLite schema has a
        // plain NOT NULL column that nothing fills, so every insert failed there;
        // assign the next value ourselves. Ties are harmless: Sequence only orders
        // rows that share an OccurredAt and carries no unique index.
        if (row.Sequence == 0 && db.Database.IsSqlite())
        {
            var last = await db.AuditLogs.MaxAsync(a => (long?)a.Sequence, ct).ConfigureAwait(false);
            row.Sequence = (last ?? 0) + 1;
        }

        db.AuditLogs.Add(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
