using Balsm.Customer.Infrastructure.Data;
using Balsm.Inventory.Infrastructure.Data;
using Balsm.POS.Infrastructure.Data;
using Balsm.Prescription.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Balsm.Stubs.Tests;

/// <summary>
/// The stub modules' DbContexts own no tables yet, but each must already
/// claim its own schema so the first real migration lands in it rather than
/// in a schema shared with another module.
/// </summary>
public sealed class StubDbContextTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public static TheoryData<string, string> Contexts() => new()
    {
        { nameof(CustomerDbContext), "customer" },
        { nameof(InventoryDbContext), "inventory" },
        { nameof(POSDbContext), "pos" },
        { nameof(PrescriptionDbContext), "prescription" },
    };

    [Theory]
    [MemberData(nameof(Contexts))]
    public void Model_OfAStubContext_UsesTheModulesOwnSchema(string contextName, string expectedSchema)
    {
        using var context = Create(contextName);

        var schema = context.Model.GetDefaultSchema();

        schema.Should().Be(expectedSchema);
    }

    public void Dispose() => _connection.Dispose();

    private DbContext Create(string contextName)
    {
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        return contextName switch
        {
            nameof(CustomerDbContext) => new CustomerDbContext(Options<CustomerDbContext>(), dispatcher),
            nameof(InventoryDbContext) => new InventoryDbContext(Options<InventoryDbContext>(), dispatcher),
            nameof(POSDbContext) => new POSDbContext(Options<POSDbContext>(), dispatcher),
            nameof(PrescriptionDbContext) => new PrescriptionDbContext(Options<PrescriptionDbContext>(), dispatcher),
            _ => throw new ArgumentOutOfRangeException(nameof(contextName), contextName, null),
        };
    }

    private DbContextOptions<T> Options<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseSqlite(_connection).Options;
}
