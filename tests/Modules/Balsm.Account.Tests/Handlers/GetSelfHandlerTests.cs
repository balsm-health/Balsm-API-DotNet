using Balsm.Account.Application.Queries;
using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Handlers;
using Balsm.Infrastructure.Encryption;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.Account.Tests.Handlers;

public sealed class GetSelfHandlerTests : IDisposable
{
    private static readonly DateOnly AdultDob = new(1990, 1, 1);
    private const string SyntheticNationalId = "0000000001";

    private readonly AccountTestDatabase _database = new();
    private readonly DobEncryptionService _encryption = AccountTestDatabase.CreateEncryption();

    [Fact]
    public async Task Handle_AccountWithoutPhi_MapsPlainFields()
    {
        await _database.SeedAccountAsync(a =>
        {
            a.ClaimHandle("jane_doe");
            a.UpdateProfile("Jane", "Doe", "Synthetic bio", "female", "SA", "+10000000000");
        });

        var result = await HandleAsync();

        result.Should().BeEquivalentTo(new GetSelfResult(
            AccountTestDatabase.UserId,
            "Jane",
            "Doe",
            "jane_doe",
            "Jane Doe",
            "Synthetic bio",
            "female",
            "SA",
            "+10000000000",
            "SA",
            "en",
            nameof(DeletionState.ACTIVE),
            DobYear: null,
            DateOfBirth: null,
            NationalId: null));
    }

    [Fact]
    public async Task Handle_AccountWithoutPhi_WritesNoAuditLog()
    {
        await _database.SeedAccountAsync();

        await HandleAsync();

        using var verify = _database.NewContext();
        (await verify.UserAccountAuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_EmptyDobCiphertext_ReturnsNoDob()
    {
        await _database.SeedAccountAsync(a => a.SetDob([]));

        var result = await HandleAsync();

        result.Should().BeEquivalentTo(new { DobYear = (int?)null, DateOfBirth = (DateOnly?)null });
    }

    [Fact]
    public async Task Handle_EncryptedDob_ReturnsDecryptedDobAndYear()
    {
        await _database.SeedAccountAsync(a => a.SetDob(_encryption.Encrypt(AdultDob)));

        var result = await HandleAsync();

        result.Should().BeEquivalentTo(new { DobYear = (int?)1990, DateOfBirth = (DateOnly?)AdultDob });
    }

    [Fact]
    public async Task Handle_EncryptedDob_WritesSelfReadAuditLog()
    {
        await _database.SeedAccountAsync(a => a.SetDob(_encryption.Encrypt(AdultDob)));

        await HandleAsync();

        using var verify = _database.NewContext();
        var log = await verify.UserAccountAuditLogs.SingleAsync();
        log.Should().BeEquivalentTo(new
        {
            TargetUserId = AccountTestDatabase.UserId,
            ActorUserId = AccountTestDatabase.UserId,
            SourceIp = (string?)null,
        });
    }

    [Fact]
    public async Task Handle_EncryptedNationalId_ReturnsDecryptedNationalId()
    {
        await _database.SeedAccountAsync(a => a.SetNationalId(_encryption.EncryptString(SyntheticNationalId)));

        var result = await HandleAsync();

        result.NationalId.Should().Be(SyntheticNationalId);
    }

    [Fact]
    public async Task Handle_EncryptedNationalIdOnly_WritesAuditLog()
    {
        await _database.SeedAccountAsync(a => a.SetNationalId(_encryption.EncryptString(SyntheticNationalId)));

        await HandleAsync();

        using var verify = _database.NewContext();
        (await verify.UserAccountAuditLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_EmptyNationalIdCiphertext_ReturnsNoNationalId()
    {
        await _database.SeedAccountAsync(a => a.SetNationalId([]));

        var result = await HandleAsync();

        result.NationalId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LegacyTwoWordDisplayName_SplitsIntoFirstAndLastName()
    {
        await SeedLegacyDisplayNameAsync("  Jane  Doe ");

        var result = await HandleAsync();

        result.Should().BeEquivalentTo(new { FirstName = "Jane", LastName = "Doe" });
    }

    [Fact]
    public async Task Handle_LegacySingleWordDisplayName_UsesItAsFirstName()
    {
        await SeedLegacyDisplayNameAsync("Jane");

        var result = await HandleAsync();

        result.Should().BeEquivalentTo(new { FirstName = "Jane", LastName = (string?)null });
    }

    [Fact]
    public async Task Handle_BlankDisplayNameAndNoNames_ReturnsNullNames()
    {
        await SeedLegacyDisplayNameAsync("   ");

        var result = await HandleAsync();

        result.Should().BeEquivalentTo(new { FirstName = (string?)null, LastName = (string?)null });
    }

    [Fact]
    public async Task Handle_OnlyLastNameSet_DoesNotSplitDisplayName()
    {
        await _database.SeedAccountAsync(a => a.UpdateProfile(lastName: "Doe"));

        var result = await HandleAsync();

        result.Should().BeEquivalentTo(new { FirstName = (string?)null, LastName = "Doe" });
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsInvalidOperation()
    {
        var handler = new GetSelfHandler(_database.Db, _encryption);

        var act = () => handler.Handle(new GetSelfQuery(AccountTestDatabase.MissingUserId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Account not found");
    }

    public void Dispose() => _database.Dispose();

    private async Task<GetSelfResult> HandleAsync()
    {
        using var db = _database.NewContext();
        var handler = new GetSelfHandler(db, _encryption);
        return await handler.Handle(new GetSelfQuery(AccountTestDatabase.UserId), CancellationToken.None);
    }

    /// <summary>Accounts created before first/last name existed carry only a
    /// display name; the domain cannot produce that shape, so write it directly.</summary>
    private async Task SeedLegacyDisplayNameAsync(string displayName)
    {
        await _database.SeedAccountAsync();
        await _database.Db.UserAccounts.ExecuteUpdateAsync(s => s.SetProperty(a => a.DisplayName, displayName));
    }
}
