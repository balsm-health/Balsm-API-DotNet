using Balsm.Account.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Balsm.Account.Tests.Domain;

public sealed class UserAccountAuditLogTests
{
    private static readonly Guid TargetId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid CorrelationId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    [Fact]
    public void Create_WithIds_RecordsTargetActorCorrelationAndIp()
    {
        var log = UserAccountAuditLog.Create(TargetId, ActorId, CorrelationId, "192.0.2.1");

        log.Should().BeEquivalentTo(new
        {
            TargetUserId = TargetId,
            ActorUserId = ActorId,
            CorrelationId,
            SourceIp = "192.0.2.1",
        });
    }

    [Fact]
    public void Create_WithoutSourceIp_LeavesItNull()
    {
        var log = UserAccountAuditLog.Create(TargetId, ActorId, CorrelationId, sourceIp: null);

        log.SourceIp.Should().BeNull();
    }

    [Fact]
    public void Create_NewEntry_AssignsId()
    {
        var log = UserAccountAuditLog.Create(TargetId, ActorId, CorrelationId, sourceIp: null);

        log.Id.Should().NotBeEmpty();
    }
}
