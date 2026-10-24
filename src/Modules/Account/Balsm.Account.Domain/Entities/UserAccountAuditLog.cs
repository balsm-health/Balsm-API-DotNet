namespace Balsm.Account.Domain.Entities;

public sealed class UserAccountAuditLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TargetUserId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTime ReadAt { get; private set; } = DateTime.UtcNow;
    public string? SourceIp { get; private set; }
    public Guid CorrelationId { get; private set; }

    private UserAccountAuditLog() { }

    public static UserAccountAuditLog Create(Guid targetUserId, Guid actorUserId, Guid correlationId, string? sourceIp) =>
        new() { TargetUserId = targetUserId, ActorUserId = actorUserId, CorrelationId = correlationId, SourceIp = sourceIp };
}
