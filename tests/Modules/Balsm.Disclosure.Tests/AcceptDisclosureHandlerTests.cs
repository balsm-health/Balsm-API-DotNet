using Balsm.Disclosure.Application.Commands;
using Balsm.Disclosure.Domain.Entities;
using Balsm.Disclosure.Infrastructure.Handlers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.Disclosure.Tests;

public sealed class AcceptDisclosureHandlerTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly DisclosureTestDb _db = new();

    public void Dispose() => _db.Dispose();

    private static AcceptDisclosureCommand Command(
        Guid? userId = null, string disclosureId = "privacy-notice", string version = "2026-01") =>
        new(userId ?? UserId, disclosureId, version, "EG", "Test Authority", "ar-EG");

    private async Task<AcceptDisclosureResult> AcceptAsync(AcceptDisclosureCommand command)
    {
        await using var context = _db.CreateContext();
        return await new AcceptDisclosureHandler(context).Handle(command, CancellationToken.None);
    }

    private async Task<List<DisclosureAcceptance>> AllAcceptancesAsync()
    {
        await using var context = _db.CreateContext();
        return await context.DisclosureAcceptances.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task Handle_WhenFirstAcceptance_ReportsNotAlreadyAccepted()
    {
        var result = await AcceptAsync(Command());

        result.WasAlreadyAccepted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenFirstAcceptance_PersistsSnapshotOfCommand()
    {
        var result = await AcceptAsync(Command());

        (await AllAcceptancesAsync()).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Id = result.AcceptanceId,
            UserId,
            DisclosureId = "privacy-notice",
            Version = "2026-01",
            CountryCodeAtAccept = "EG",
            SupervisoryAuthorityNameAtAccept = "Test Authority",
            PreferredLanguageAtAccept = "ar-EG",
        });
    }

    [Fact]
    public async Task Handle_WhenSameVersionAcceptedAgain_ReturnsOriginalAcceptance()
    {
        var first = await AcceptAsync(Command());

        var second = await AcceptAsync(Command());

        second.Should().Be(new AcceptDisclosureResult(first.AcceptanceId, WasAlreadyAccepted: true));
    }

    [Fact]
    public async Task Handle_WhenSameVersionAcceptedAgain_DoesNotInsertDuplicate()
    {
        await AcceptAsync(Command());

        await AcceptAsync(Command());

        (await AllAcceptancesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WhenNewVersionAccepted_RecordsSeparateAcceptance()
    {
        await AcceptAsync(Command(version: "2026-01"));

        var result = await AcceptAsync(Command(version: "2026-02"));

        result.WasAlreadyAccepted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenDifferentDisclosureAccepted_RecordsSeparateAcceptance()
    {
        await AcceptAsync(Command(disclosureId: "privacy-notice"));

        var result = await AcceptAsync(Command(disclosureId: "terms-of-use"));

        result.WasAlreadyAccepted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenAnotherUserAcceptedSameVersion_RecordsSeparateAcceptance()
    {
        await AcceptAsync(Command(userId: OtherUserId));

        var result = await AcceptAsync(Command(userId: UserId));

        result.WasAlreadyAccepted.Should().BeFalse();
    }
}
