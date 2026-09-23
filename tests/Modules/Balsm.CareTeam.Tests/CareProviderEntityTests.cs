using Balsm.CareTeam.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Balsm.CareTeam.Tests;

public sealed class CareProviderEntityTests
{
    private static CareProviderFields Fields(string marker = "a") =>
        new([(byte)marker[0]], null, null, null, null, null, null, null, null);

    [Fact]
    public void Create_WithClientId_KeepsThatId()
    {
        var id = Guid.NewGuid();
        var provider = CareProvider.Create(id, Guid.NewGuid(), Guid.NewGuid(), "doctor", Fields(), DateTime.UtcNow);
        provider.Id.Should().Be(id);
    }

    [Fact]
    public void Create_WithEmptyId_Throws()
    {
        Action act = () => CareProvider.Create(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "doctor", Fields(), DateTime.UtcNow);
        act.Should().Throw<ArgumentException>().WithMessage("*empty GUID*");
    }

    [Fact]
    public void Create_WithUnknownType_Throws()
    {
        Action act = () => CareProvider.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "astrologer", Fields(), DateTime.UtcNow);
        act.Should().Throw<ArgumentException>().WithMessage("*Invalid care provider type*");
    }

    [Fact]
    public void Overwrite_ReplacesFieldsAndType()
    {
        var provider = CareProvider.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "doctor", Fields("a"), DateTime.UtcNow);
        provider.Overwrite("pharmacy", Fields("b"));
        provider.Type.Should().Be("pharmacy");
        provider.Name.Should().Equal((byte)'b');
    }

    [Fact]
    public void Overwrite_OnTombstonedRow_Throws()
    {
        var provider = CareProvider.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "doctor", Fields(), DateTime.UtcNow);
        provider.Tombstone();
        Action act = () => provider.Overwrite("doctor", Fields("b"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*tombstoned*");
    }

    [Fact]
    public void Tombstone_IsIdempotent()
    {
        var provider = CareProvider.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "doctor", Fields(), DateTime.UtcNow);
        provider.Tombstone();
        var first = provider.DeletedAt;
        provider.Tombstone();
        provider.DeletedAt.Should().Be(first);
    }
}
