using Xunit;
using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Deletion.Application.Commands;
using Balsm.Deletion.Infrastructure.Data;
using Balsm.Deletion.Infrastructure.Handlers;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Balsm.Deletion.Tests;

/// <summary>
/// T150: Deletion FSM — intake / cancel / grace expiry state transitions. FR-031/032.
/// </summary>
public sealed class DeletionFsmTests : IDisposable
{
    private readonly AccountDbContext _accountDb;
    private readonly DeletionDbContext _deletionDb;

    public DeletionFsmTests()
    {
        var dispatcher = Substitute.For<IDomainEventDispatcher>();

        var accountOpts = new DbContextOptionsBuilder<AccountDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _accountDb = new AccountDbContext(accountOpts, dispatcher);
        _accountDb.Database.EnsureCreated();

        var deletionOpts = new DbContextOptionsBuilder<DeletionDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _deletionDb = new DeletionDbContext(deletionOpts, dispatcher);
        _deletionDb.Database.EnsureCreated();
    }

    [Fact]
    public async Task Intake_SetsState_DeletionRequested()
    {
        var account = UserAccount.Create("EG", "ar-EG");
        _accountDb.UserAccounts.Add(account);
        await _accountDb.SaveChangesAsync();

        var handler = new IntakeDeletionHandler(_deletionDb, _accountDb);
        await handler.Handle(new IntakeDeletionCommand(account.Id, "EG", null), CancellationToken.None);

        var updated = await _accountDb.UserAccounts.FindAsync(account.Id);
        updated!.DeletionState.Should().Be(DeletionState.DELETION_REQUESTED);
        updated.DeletionGraceUntil.Should().NotBeNull();
        updated.DeletionGraceUntil!.Value.Should().BeCloseTo(
            DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Intake_CreatesDeletionLog()
    {
        var account = UserAccount.Create("AE", "en");
        _accountDb.UserAccounts.Add(account);
        await _accountDb.SaveChangesAsync();

        var handler = new IntakeDeletionHandler(_deletionDb, _accountDb);
        await handler.Handle(new IntakeDeletionCommand(account.Id, "AE", "user_request"), CancellationToken.None);

        var log = await _deletionDb.DeletionLogs.FirstOrDefaultAsync();
        log.Should().NotBeNull();
        log!.CountryCodeAtDeletion.Should().Be("AE");
        log.ReasonCode.Should().Be("user_request");
        log.PurgeAt.Should().BeAfter(DateTime.UtcNow.AddYears(1));
    }

    [Fact]
    public async Task Intake_Idempotent_DoesNotDuplicate()
    {
        var account = UserAccount.Create("SA", "ar-SA");
        _accountDb.UserAccounts.Add(account);
        await _accountDb.SaveChangesAsync();

        var handler = new IntakeDeletionHandler(_deletionDb, _accountDb);
        await handler.Handle(new IntakeDeletionCommand(account.Id, "SA", null), CancellationToken.None);
        await handler.Handle(new IntakeDeletionCommand(account.Id, "SA", null), CancellationToken.None);

        var logs = await _deletionDb.DeletionLogs.ToListAsync();
        logs.Should().HaveCount(1, "idempotent — second intake ignored");
    }

    [Fact]
    public async Task Cancel_WithinGrace_RestoresActiveState()
    {
        var account = UserAccount.Create("EG", "ar-EG");
        _accountDb.UserAccounts.Add(account);
        await _accountDb.SaveChangesAsync();

        var intakeHandler = new IntakeDeletionHandler(_deletionDb, _accountDb);
        await intakeHandler.Handle(new IntakeDeletionCommand(account.Id, "EG", null), CancellationToken.None);

        var cancelHandler = new CancelDeletionHandler(_accountDb);
        await cancelHandler.Handle(new CancelDeletionCommand(account.Id), CancellationToken.None);

        var updated = await _accountDb.UserAccounts.FindAsync(account.Id);
        updated!.DeletionState.Should().Be(DeletionState.DELETION_CANCELLED,
            "cancellation within grace period should reset to CANCELLED");
    }

    [Fact]
    public async Task Cancel_WhenNotRequested_Throws()
    {
        var account = UserAccount.Create("EG", "ar-EG");
        _accountDb.UserAccounts.Add(account);
        await _accountDb.SaveChangesAsync();

        var cancelHandler = new CancelDeletionHandler(_accountDb);
        var act = () => cancelHandler.Handle(new CancelDeletionCommand(account.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "cannot cancel a deletion that was not requested");
    }

    public void Dispose()
    {
        _accountDb.Database.EnsureDeleted();
        _accountDb.Dispose();
        _deletionDb.Database.EnsureDeleted();
        _deletionDb.Dispose();
    }
}
