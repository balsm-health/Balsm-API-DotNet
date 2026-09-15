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
/// Permanent (non-expiring) medical-profile QR: ttl_seconds = 0 mints a token
/// with no expiry; the QR URL stays stable while the ciphertext is refreshed
/// in place so a scan always shows current data.
/// </summary>
public sealed class PermanentQrTests : IDisposable
{
    private readonly EmergencyQrDbContext _db;
    private readonly SqliteConnection _connection;
    private static readonly byte[] SampleCiphertext = new byte[64];

    public PermanentQrTests()
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

    [Fact]
    public async Task MintPermanent_HasNoExpiry_AndResolves()
    {
        var userId = Guid.NewGuid();
        var mint = await new MintEmergencyQrHandler(_db).Handle(
            new MintEmergencyQrCommand(userId, SampleCiphertext, "etag1", EmergencyQrToken.PermanentTtlSeconds),
            CancellationToken.None);

        mint.ExpiresAt.Should().BeNull("permanent tokens never expire");

        var resolved = await new ResolveEmergencyQrHandler(_db).Handle(
            new ResolveEmergencyQrQuery(mint.TokenId), CancellationToken.None);

        resolved.Should().NotBeNull();
        resolved!.ExpiresAt.Should().BeNull();
        resolved.Ciphertext.Should().BeEquivalentTo(SampleCiphertext);
    }

    [Fact]
    public async Task MintPermanent_AppearsAsActive()
    {
        var userId = Guid.NewGuid();
        var mint = await new MintEmergencyQrHandler(_db).Handle(
            new MintEmergencyQrCommand(userId, SampleCiphertext, "etag1", 0),
            CancellationToken.None);

        var active = await new GetActiveQrHandler(_db).Handle(
            new GetActiveQrQuery(userId), CancellationToken.None);

        active.Should().NotBeNull();
        active!.TokenId.Should().Be(mint.TokenId);
        active.ExpiresAt.Should().BeNull();
        active.TtlSeconds.Should().Be(0);
    }

    [Fact]
    public async Task UpdateCiphertext_ByOwner_ResolvesNewCiphertext()
    {
        var userId = Guid.NewGuid();
        var mint = await new MintEmergencyQrHandler(_db).Handle(
            new MintEmergencyQrCommand(userId, SampleCiphertext, "etag1", 0),
            CancellationToken.None);

        var newCiphertext = new byte[64];
        newCiphertext[0] = 0xFF;
        await new UpdateEmergencyQrCiphertextHandler(_db).Handle(
            new UpdateEmergencyQrCiphertextCommand(mint.TokenId, userId, newCiphertext, "etag2"),
            CancellationToken.None);

        var resolved = await new ResolveEmergencyQrHandler(_db).Handle(
            new ResolveEmergencyQrQuery(mint.TokenId), CancellationToken.None);

        resolved!.Ciphertext.Should().BeEquivalentTo(newCiphertext);
        resolved.Type.Should().Be(EmergencyQrToken.ProfileType);
    }

    [Fact]
    public async Task UpdateCiphertext_ByOtherUser_Throws()
    {
        var owner = Guid.NewGuid();
        var mint = await new MintEmergencyQrHandler(_db).Handle(
            new MintEmergencyQrCommand(owner, SampleCiphertext, "etag1", 0),
            CancellationToken.None);

        Func<Task> act = () => new UpdateEmergencyQrCiphertextHandler(_db).Handle(
            new UpdateEmergencyQrCiphertextCommand(mint.TokenId, Guid.NewGuid(), SampleCiphertext, "etag2"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UpdateCiphertext_OnRevokedToken_Throws()
    {
        var userId = Guid.NewGuid();
        var mint = await new MintEmergencyQrHandler(_db).Handle(
            new MintEmergencyQrCommand(userId, SampleCiphertext, "etag1", 0),
            CancellationToken.None);
        await new RevokeEmergencyQrHandler(_db).Handle(
            new RevokeEmergencyQrCommand(mint.TokenId, userId), CancellationToken.None);

        Func<Task> act = () => new UpdateEmergencyQrCiphertextHandler(_db).Handle(
            new UpdateEmergencyQrCiphertextCommand(mint.TokenId, userId, SampleCiphertext, "etag2"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task MintPermanent_RevokesPriorTemporaryToken()
    {
        var userId = Guid.NewGuid();
        var first = await new MintEmergencyQrHandler(_db).Handle(
            new MintEmergencyQrCommand(userId, SampleCiphertext, "etag1", 3600),
            CancellationToken.None);
        var second = await new MintEmergencyQrHandler(_db).Handle(
            new MintEmergencyQrCommand(userId, SampleCiphertext, "etag1", 0),
            CancellationToken.None);

        (await new ResolveEmergencyQrHandler(_db).Handle(
            new ResolveEmergencyQrQuery(first.TokenId), CancellationToken.None)).Should().BeNull();
        (await new ResolveEmergencyQrHandler(_db).Handle(
            new ResolveEmergencyQrQuery(second.TokenId), CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task Mint_WithClientTokenId_UsesIt()
    {
        var userId = Guid.NewGuid();
        var jti = Guid.NewGuid();

        var result = await new MintEmergencyQrHandler(_db).Handle(
            new MintEmergencyQrCommand(userId, SampleCiphertext, "etag1", 0, jti),
            CancellationToken.None);

        result.TokenId.Should().Be(jti);
    }

    [Fact]
    public async Task Mint_RetrySameTokenId_IsIdempotentAndRefreshesCiphertext()
    {
        var userId = Guid.NewGuid();
        var jti = Guid.NewGuid();
        await new MintEmergencyQrHandler(_db).Handle(
            new MintEmergencyQrCommand(userId, SampleCiphertext, "etag1", 0, jti),
            CancellationToken.None);

        var newCiphertext = new byte[64];
        newCiphertext[0] = 0xAB;
        var retry = await new MintEmergencyQrHandler(_db).Handle(
            new MintEmergencyQrCommand(userId, newCiphertext, "etag2", 0, jti),
            CancellationToken.None);

        retry.TokenId.Should().Be(jti);
        _db.EmergencyQrTokens.Count(t => t.Id == jti).Should().Be(1);
        var resolved = await new ResolveEmergencyQrHandler(_db).Handle(
            new ResolveEmergencyQrQuery(jti), CancellationToken.None);
        resolved!.Ciphertext.Should().BeEquivalentTo(newCiphertext);
    }

    [Fact]
    public async Task Mint_ClientTokenIdOwnedByAnotherUser_Throws()
    {
        var jti = Guid.NewGuid();
        await new MintEmergencyQrHandler(_db).Handle(
            new MintEmergencyQrCommand(Guid.NewGuid(), SampleCiphertext, "etag1", 0, jti),
            CancellationToken.None);

        Func<Task> act = () => new MintEmergencyQrHandler(_db).Handle(
            new MintEmergencyQrCommand(Guid.NewGuid(), SampleCiphertext, "etag1", 0, jti),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public void Mint_InvalidTtl_StillRejected()
    {
        var act = () => EmergencyQrToken.Mint(Guid.NewGuid(), SampleCiphertext, "etag1", 1234);
        act.Should().Throw<ArgumentException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
