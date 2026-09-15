using Xunit;
using Balsm.EmergencyQr.Application.Commands;
using Balsm.EmergencyQr.Application.Queries;
using Balsm.EmergencyQr.Domain.Entities;
using Balsm.EmergencyQr.Infrastructure.Data;
using Balsm.EmergencyQr.Infrastructure.Handlers;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Balsm.EmergencyQr.Tests;

/// <summary>
/// Spec v2.0 scan history: each successful public resolve is recorded for the
/// token's owner (time + coarse client class, never scanner identity); failed
/// resolves record nothing; owners see only their own history.
/// </summary>
public sealed class ScanHistoryTests : IDisposable
{
    private readonly EmergencyQrDbContext _db;
    private readonly SqliteConnection _connection;
    private static readonly byte[] SampleCiphertext = new byte[64];

    public ScanHistoryTests()
    {
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var opts = new DbContextOptionsBuilder<EmergencyQrDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new EmergencyQrDbContext(opts, dispatcher);
        _db.Database.EnsureCreated();
    }

    private async Task<Guid> MintPermanentAsync(Guid userId)
    {
        var mint = (await new MintEmergencyQrHandler(_db).Handle(
            new MintEmergencyQrCommand(userId, SampleCiphertext, "etag1", EmergencyQrToken.PermanentTtlSeconds),
            CancellationToken.None)).Value!;
        return mint.TokenId;
    }

    [Fact]
    public async Task Resolve_Active_RecordsScanForOwner()
    {
        var owner = Guid.NewGuid();
        var jti = await MintPermanentAsync(owner);

        await new ResolveEmergencyQrHandler(_db).Handle(
            new ResolveEmergencyQrQuery(jti, "web"), CancellationToken.None);

        var scan = await _db.QrScanRecords.SingleAsync();
        scan.TokenId.Should().Be(jti);
        scan.OwnerUserId.Should().Be(owner);
        scan.ClientClass.Should().Be("web");
        scan.Country.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_UnknownOrRevoked_RecordsNothing()
    {
        var owner = Guid.NewGuid();
        var jti = await MintPermanentAsync(owner);

        await new ResolveEmergencyQrHandler(_db).Handle(
            new ResolveEmergencyQrQuery(Guid.NewGuid(), "web"), CancellationToken.None);

        await new RevokeEmergencyQrHandler(_db).Handle(
            new RevokeEmergencyQrCommand(jti, owner), CancellationToken.None);
        await new ResolveEmergencyQrHandler(_db).Handle(
            new ResolveEmergencyQrQuery(jti, "web"), CancellationToken.None);

        (await _db.QrScanRecords.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Resolve_ReturnsTypeAndNullExpiry_ForPermanent()
    {
        var jti = await MintPermanentAsync(Guid.NewGuid());

        var resolved = await new ResolveEmergencyQrHandler(_db).Handle(
            new ResolveEmergencyQrQuery(jti, "app"), CancellationToken.None);

        resolved!.Type.Should().Be(EmergencyQrToken.ProfileType);
        resolved.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task GetScans_ReturnsOwnScansNewestFirst_NeverOthers()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var aliceJti = await MintPermanentAsync(alice);
        var bobJti = await MintPermanentAsync(bob);

        var resolve = new ResolveEmergencyQrHandler(_db);
        await resolve.Handle(new ResolveEmergencyQrQuery(aliceJti, "web"), CancellationToken.None);
        await resolve.Handle(new ResolveEmergencyQrQuery(aliceJti, "app"), CancellationToken.None);
        await resolve.Handle(new ResolveEmergencyQrQuery(bobJti, "web"), CancellationToken.None);

        var scans = await new GetQrScansHandler(_db).Handle(
            new GetQrScansQuery(alice), CancellationToken.None);

        scans.Should().HaveCount(2);
        scans.Should().OnlyContain(s => s.TokenId == aliceJti);
        scans.Should().BeInDescendingOrder(s => s.ResolvedAt);
    }

    [Fact]
    public async Task GetScans_RespectsLimit()
    {
        var owner = Guid.NewGuid();
        var jti = await MintPermanentAsync(owner);
        var resolve = new ResolveEmergencyQrHandler(_db);
        for (var i = 0; i < 5; i++)
            await resolve.Handle(new ResolveEmergencyQrQuery(jti, "web"), CancellationToken.None);

        var scans = await new GetQrScansHandler(_db).Handle(
            new GetQrScansQuery(owner, Limit: 3), CancellationToken.None);

        scans.Should().HaveCount(3);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
