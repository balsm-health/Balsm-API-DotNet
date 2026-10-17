namespace Balsm.Infrastructure.Audit;

public sealed record AuditContextValue(string? Actor, string? SourceIp, string? CorrelationId);

public static class AuditContext
{
    private static readonly AsyncLocal<AuditContextValue?> _current = new();

    public static AuditContextValue? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
