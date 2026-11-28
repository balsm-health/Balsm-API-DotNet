using System.Net;
using System.Net.Http.Headers;
using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Deletion.Infrastructure.Data;
using Balsm.Deletion.Infrastructure.Jobs;
using Balsm.Infrastructure.Auth;
using Balsm.SharedKernel.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Balsm.E2E.Tests.Deletion;

/// <summary>
/// Account deletion for a Sign in with Apple user across Auth, Deletion and
/// Account: the app asks for deletion over HTTP, the grace period runs out, and
/// the purge revokes the Apple grant (fake revoker: no call to Apple), records
/// the outcome on the deletion log, then erases the auth data and the account.
/// A failed revoke keeps everything for the next night's retry.
/// </summary>
public sealed class AppleRevokePurgeE2ETests : IClassFixture<E2EWebAppFactory>
{
    private readonly WebApplicationFactory<Program> _host;
    private readonly FakeRevoker _revoker = new();

    public AppleRevokePurgeE2ETests(E2EWebAppFactory factory) =>
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAppleGrantRevoker>();
            services.AddSingleton<IAppleGrantRevoker>(_revoker);
        }));

    private async Task<(Guid Id, HttpClient Client)> SignInAppleUserAsync()
    {
        using var scope = _host.Services.CreateScope();
        var id = await scope.ServiceProvider.GetRequiredService<IUserAccountProvisioner>().ProvisionAsync("EG", "en");
        var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        authDb.UserIdentities.Add(UserIdentity.Create(id, "apple", $"001234.{Guid.NewGuid():N}.0001", null));
        authDb.UserRefreshTokens.Add(UserRefreshToken.Create(id, $"hash-{id:N}", Guid.NewGuid(), DateTime.UtcNow.AddDays(30)));
        await authDb.SaveChangesAsync();
        var token = scope.ServiceProvider.GetRequiredService<JwtService>().IssueAccessToken(id, string.Empty);
        var client = _host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (id, client);
    }

    /// Requests deletion through the API, then moves the grace end into the past.
    private async Task RequestDeletionPastGraceAsync(Guid userId, HttpClient client, int graceEndedDaysAgo)
    {
        (await client.PostAsync("/deletion/intake", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _host.Services.CreateScope();
        var accountDb = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var account = await accountDb.UserAccounts.SingleAsync(a => a.Id == userId);
        account.DeletionState.Should().Be(DeletionState.DELETION_REQUESTED);
        account.RequestDeletion(DateTime.UtcNow.AddDays(-graceEndedDaysAgo));
        await accountDb.SaveChangesAsync();
    }

    private Task RunNightlyPurgeAsync() =>
        _host.Services.GetServices<IHostedService>().OfType<DeletionPurgeJob>().Single()
            .PurgeAsync(CancellationToken.None);

    private async Task<(bool Account, bool Identity, int RefreshTokens, string? RevokeStatus)> StateAsync(Guid userId)
    {
        using var scope = _host.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var authDb = sp.GetRequiredService<AuthDbContext>();
        var hash = DeletionPurgeJob.ComputeHash(userId);
        return (
            await sp.GetRequiredService<AccountDbContext>().UserAccounts.AnyAsync(a => a.Id == userId),
            await authDb.UserIdentities.AnyAsync(i => i.UserId == userId),
            await authDb.UserRefreshTokens.CountAsync(t => t.UserId == userId),
            (await sp.GetRequiredService<DeletionDbContext>().DeletionLogs.AsNoTracking()
                .SingleAsync(l => l.UserIdHash == hash)).AppleRevokeStatus);
    }

    [Fact]
    public async Task IntakeThenPurge_RevokesAppleGrantThenErasesAuthAndAccount()
    {
        var (userId, client) = await SignInAppleUserAsync();
        _revoker.Results[userId] = "revoked";
        await RequestDeletionPastGraceAsync(userId, client, graceEndedDaysAgo: 1);

        await RunNightlyPurgeAsync();

        _revoker.Calls.Should().Contain(userId);
        (await StateAsync(userId)).Should().Be((false, false, 0, "succeeded"));
    }

    [Fact]
    public async Task IntakeThenPurge_RevokeFails_KeepsAccountAndTokenForRetryThenPurgesOnSuccess()
    {
        var (userId, client) = await SignInAppleUserAsync();
        _revoker.Results[userId] = "failed";
        await RequestDeletionPastGraceAsync(userId, client, graceEndedDaysAgo: 1);

        await RunNightlyPurgeAsync();
        var afterFailure = await StateAsync(userId);
        _revoker.Results[userId] = "revoked";
        await RunNightlyPurgeAsync();

        afterFailure.Should().Be((true, true, 1, "failed_retrying"));
        (await StateAsync(userId)).Should().Be((false, false, 0, "succeeded"));
    }

    [Fact]
    public async Task IntakeThenPurge_RevokeFailsPastRetryWindow_RecordsFinalAndPurges()
    {
        var (userId, client) = await SignInAppleUserAsync();
        _revoker.Results[userId] = "failed";
        await RequestDeletionPastGraceAsync(userId, client, graceEndedDaysAgo: 6);

        await RunNightlyPurgeAsync();

        (await StateAsync(userId)).Should().Be((false, false, 0, "failed_final"));
    }

    private sealed class FakeRevoker : IAppleGrantRevoker
    {
        public Dictionary<Guid, string> Results { get; } = [];
        public List<Guid> Calls { get; } = [];

        public Task<string> RevokeForAccountAsync(Guid userId, CancellationToken ct = default)
        {
            lock (Calls) Calls.Add(userId);
            return Task.FromResult(Results.GetValueOrDefault(userId, "no_apple_identity"));
        }
    }
}
