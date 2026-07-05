using Balsm.Account.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balsm.Account.Infrastructure.Configuration;

internal sealed class AccountDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AccountDbContext>
{
    public AccountDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<AccountDbContext>();
        builder.UseNpgsql("Host=localhost;Database=design;Username=design;Password=design");
        return new AccountDbContext(builder.Options, new NullDomainEventDispatcher());
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
