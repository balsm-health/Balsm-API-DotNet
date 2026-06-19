using Xunit;
using Balsm.EmergencyQr.Application.Commands;
using Balsm.EmergencyQr.Application.Queries;
using Balsm.EmergencyQr.Domain.Entities;
using Balsm.EmergencyQr.Infrastructure.Data;
using Balsm.EmergencyQr.Infrastructure.Handlers;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Balsm.EmergencyQr.Tests;

/// <summary>
/// T121b: mint→resolve, revoke→410, second-mint-revokes-first. SC-014.
/// </summary>
public sealed class QrLifecycleTests : IDisposable
{
    private readonly EmergencyQrDbContext _db;
    private static readonly byte[] SampleCiphertext = new byte[64];
    private const int TtlSeconds = 3600;

    public QrLifecycleTests()
    {
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        var opts = new DbContextOptionsBuilder<EmergencyQrDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _db = new EmergencyQrDbContext(opts, dispatcher);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Mint_ThenResolve_ReturnsActiveCiphertext()
    {
        var userId = Guid.NewGuid();
        var mintHandler = new MintEmergencyQrHandler(_db);
        var mintResult = await mintHandler.Handle(
            new MintEmergencyQrCommand(userId, SampleCiphertext, "etag1", "en", TtlSeconds),
            CancellationToken.None);

        mintResult.TokenId.Should().NotBeEmpty();
        mintResult.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var resolveHandler = new ResolveEmergencyQrHandler(_db);
        var resolved = await resolveHandler.Handle(
            new ResolveEmergencyQrQuery(mintResult.TokenId),
            CancellationToken.None);

        resolved.Should().NotBeNull("active token should resolve");
        resolved!.Ciphertext.Should().BeEquivalentTo(SampleCiphertext);
        resolved.PreferredLanguage.Should().Be("en");
    }

    [Fact]
    public async Task Revoke_ThenResolve_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var mintHandler = new MintEmergencyQrHandler(_db);
        var mintResult = await mintHandler.Handle(
            new MintEmergencyQrCommand(userId, SampleCiphertext, "etag1", "ar-EG", TtlSeconds),
            CancellationToken.None);

        var revokeHandler = new RevokeEmergencyQrHandler(_db);
        await revokeHandler.Handle(
            new RevokeEmergencyQrCommand(mintResult.TokenId, userId),
            CancellationToken.None);

        var resolveHandler = new ResolveEmergencyQrHandler(_db);
        var resolved = await resolveHandler.Handle(
            new ResolveEmergencyQrQuery(mintResult.TokenId),
            CancellationToken.None);

        resolved.Should().BeNull("revoked token should return null (controller emits 410)");
    }

    [Fact]
    public async Task SecondMint_RevokesFirstToken()
    {
        var userId = Guid.NewGuid();
        var mintHandler = new MintEmergencyQrHandler(_db);

        var first = await mintHandler.Handle(
            new MintEmergencyQrCommand(userId, SampleCiphertext, "etag1", "en", TtlSeconds),
            CancellationToken.None);

        var ciphertext2 = new byte[64];
        ciphertext2[0] = 0xFF;
        var second = await mintHandler.Handle(
            new MintEmergencyQrCommand(userId, ciphertext2, "etag2", "en", TtlSeconds),
            CancellationToken.None);

        // First token should now be revoked
        var firstToken = await _db.EmergencyQrTokens.FindAsync(first.TokenId);
        firstToken!.IsActive.Should().BeFalse("second mint should revoke first");
        firstToken.RevokedAt.Should().NotBeNull();

        // Second token should be active
        var secondToken = await _db.EmergencyQrTokens.FindAsync(second.TokenId);
        secondToken!.IsActive.Should().BeTrue("second mint should be active");
    }

    [Fact]
    public async Task Revoke_ByDifferentUser_ThrowsUnauthorized()
    {
        var userId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();

        var mintHandler = new MintEmergencyQrHandler(_db);
        var mintResult = await mintHandler.Handle(
            new MintEmergencyQrCommand(userId, SampleCiphertext, "etag1", "en", TtlSeconds),
            CancellationToken.None);

        var revokeHandler = new RevokeEmergencyQrHandler(_db);
        var act = () => revokeHandler.Handle(
            new RevokeEmergencyQrCommand(mintResult.TokenId, otherUser),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "only the owning user should revoke their own QR token");
    }

    [Fact]
    public void Mint_InvalidTtl_Throws()
    {
        var act = () => EmergencyQrToken.Mint(Guid.NewGuid(), SampleCiphertext, "etag", "en", 999);
        act.Should().Throw<ArgumentException>("999 is not in AllowedTtlSeconds");
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}
