namespace Balsm.Infrastructure.Audit;

public interface IAuditLogWriter
{
    Task WriteAsync(AuditLog row, CancellationToken ct = default);
}
