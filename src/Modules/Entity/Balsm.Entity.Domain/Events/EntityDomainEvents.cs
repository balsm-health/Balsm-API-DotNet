using Balsm.SharedKernel.Events;

namespace Balsm.Entity.Domain.Events;

public abstract record DomainEventBase : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public sealed record WorkspaceUpdatedEvent(Guid WorkspaceId, string Name) : DomainEventBase;
public sealed record WorkspaceSlugChangedEvent(Guid WorkspaceId, string NewSlug) : DomainEventBase;
public sealed record EntityCreatedEvent(Guid EntityId, Guid WorkspaceId, string Name) : DomainEventBase;
public sealed record EntityUpdatedEvent(Guid EntityId, string Name) : DomainEventBase;
public sealed record EntityDeactivatedEvent(Guid EntityId) : DomainEventBase;
public sealed record EntityReactivatedEvent(Guid EntityId) : DomainEventBase;
public sealed record BranchCreatedEvent(Guid BranchId, Guid EntityRootId, string Name) : DomainEventBase;
public sealed record BranchUpdatedEvent(Guid BranchId, string Name) : DomainEventBase;
public sealed record BranchDeactivatedEvent(Guid BranchId) : DomainEventBase;
public sealed record BranchReactivatedEvent(Guid BranchId) : DomainEventBase;
