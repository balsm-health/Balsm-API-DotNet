using System.Linq.Expressions;
using System.Reflection;
using Balsm.Infrastructure.Audit;
using Balsm.SharedKernel.Domain;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Infrastructure.Data;

public abstract class BaseDbContext(
    DbContextOptions options,
    IDomainEventDispatcher domainEventDispatcher) : DbContext(options)
{
    /// <remarks>
    /// Derived contexts must call this AFTER applying their entity configurations —
    /// the soft-delete filters and provider fixups below inspect the configured model.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            // Entities may opt out of soft-delete by not mapping IsDeleted
            // (e.g. revocation-based tables); a filter on an unmapped member
            // makes every query on the entity untranslatable.
            if (entityType.FindProperty(nameof(BaseEntity.IsDeleted)) is null)
                continue;

            // A filter declared in the entity's own configuration wins.
            if (entityType.GetDeclaredQueryFilters().Count > 0)
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
            var condition = Expression.Equal(property, Expression.Constant(false));
            var lambda = Expression.Lambda(condition, parameter);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }

        // Cloud modules declare PostgreSQL column defaults; SQLite cannot evaluate
        // them at insert time, so translate to equivalents it understands.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
            {
                switch (property.GetDefaultValueSql())
                {
                    case "now()":
                        property.SetDefaultValueSql("CURRENT_TIMESTAMP");
                        break;
                    case "gen_random_uuid()":
                        property.SetDefaultValueSql(null);
                        property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd;
                        break;
                }
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditFields();
        var result = await base.SaveChangesAsync(cancellationToken);
        await DispatchDomainEventsAsync(cancellationToken);
        return result;
    }

    private void SetAuditFields()
    {
        var utcNow = DateTime.UtcNow;
        var actor = AuditContext.Current?.Actor;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = utcNow;
                    entry.Entity.CreatedBy = actor;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = utcNow;
                    entry.Entity.UpdatedBy = actor;
                    // Handle soft-delete toggle
                    if (entry.Entity.IsDeleted && entry.Entity.DeletedAt is null)
                    {
                        entry.Entity.DeletedAt = utcNow;
                        entry.Entity.DeletedBy = actor;
                    }
                    break;
            }
        }
    }

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        var aggregateRoots = ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = aggregateRoots.SelectMany(a => a.DomainEvents).ToList();
        aggregateRoots.ForEach(a => a.ClearDomainEvents());

        await domainEventDispatcher.DispatchEventsAsync(domainEvents, cancellationToken);
    }
}
