using Balsm.EmergencyQr.Domain.Entities;
using Balsm.EmergencyQr.Infrastructure.Data;
using Balsm.EmergencyQr.Infrastructure.Services;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Balsm.EmergencyQr.Tests;

/// <summary>The jti → owner contract family linking relies on: only an active profile token resolves.</summary>
public sealed class ProfileQrOwnerResolverTests : IDisposable
{
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly EmergencyQrDbContext _db;

    public ProfileQrOwnerResolverTests()
    {
        _connection.Open();
        _db = new EmergencyQrDbContext(
            new DbContextOptionsBuilder<EmergencyQrDbContext>().UseSqlite(_connection).Options,
            Substitute.For<IDomainEventDispatcher>());
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed class Clock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private async Task<Guid> MintAsync(int ttlSeconds)
    {
        var token = EmergencyQrToken.Mint(Owner, [1, 2, 3], "etag", ttlSeconds, Guid.NewGuid());
        _db.EmergencyQrTokens.Add(token);
        await _db.SaveChangesAsync();
        return token.Id;
    }

    private ProfileQrOwnerResolver Resolver(DateTime? now = null) => new(_db, new Clock(now ?? DateTime.UtcNow));

    [Fact]
    public async Task ResolveOwner_ActivePermanentToken_ReturnsOwner()
    {
        var jti = await MintAsync(EmergencyQrToken.PermanentTtlSeconds);

        var owner = await Resolver().ResolveOwnerAsync(jti);

        owner.Should().Be(Owner);
    }

    [Fact]
    public async Task ResolveOwner_UnknownJti_ReturnsNull()
    {
        var owner = await Resolver().ResolveOwnerAsync(Guid.NewGuid());

        owner.Should().BeNull();
    }

    [Fact]
    public async Task ResolveOwner_RevokedToken_ReturnsNull()
    {
        var jti = await MintAsync(EmergencyQrToken.PermanentTtlSeconds);
        (await _db.EmergencyQrTokens.SingleAsync(t => t.Id == jti)).Revoke();
        await _db.SaveChangesAsync();

        var owner = await Resolver().ResolveOwnerAsync(jti);

        owner.Should().BeNull();
    }

    [Fact]
    public async Task ResolveOwner_ExpiredToken_ReturnsNull()
    {
        var jti = await MintAsync(3600);

        var owner = await Resolver(DateTime.UtcNow.AddHours(2)).ResolveOwnerAsync(jti);

        owner.Should().BeNull();
    }

    [Fact]
    public async Task ResolveOwner_DoesNotRecordAScan()
    {
        var jti = await MintAsync(EmergencyQrToken.PermanentTtlSeconds);

        await Resolver().ResolveOwnerAsync(jti);

        (await _db.QrScanRecords.AnyAsync()).Should().BeFalse();
    }
}
