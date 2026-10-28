using Balsm.Account.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Balsm.Account.Tests.Domain;

public sealed class ReservedHandleBlocklistTests
{
    [Fact]
    public void Create_WithoutAddedBy_DefaultsToSystem()
    {
        var entry = ReservedHandleBlocklist.Create("Admin");

        entry.Should().BeEquivalentTo(new { HandleNormalized = "admin", AddedBy = "system" });
    }

    [Fact]
    public void Create_WithAddedBy_RecordsIt()
    {
        var entry = ReservedHandleBlocklist.Create("support", "ops@example.test");

        entry.AddedBy.Should().Be("ops@example.test");
    }
}
