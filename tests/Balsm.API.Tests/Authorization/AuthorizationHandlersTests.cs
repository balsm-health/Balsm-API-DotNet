using System.Security.Claims;
using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.API.Authorization;
using Balsm.Infrastructure.Encryption;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.API.Tests.Authorization;

public sealed class AuthorizationHandlersTests : IDisposable
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services;
    private readonly DobEncryptionService _dob;

    public AuthorizationHandlersTests()
    {
        _connection.Open();
        _services = new ServiceCollection()
            .AddScoped<IDomainEventDispatcher, DomainEventDispatcher>()
            .AddDbContext<AccountDbContext>(o => o.UseSqlite(_connection))
            .BuildServiceProvider();
        using (var scope = _services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<AccountDbContext>().Database.EnsureCreated();
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DobEncryption:Key"] = Convert.ToBase64String(new byte[32]),
            })
            .Build();
        _dob = new DobEncryptionService(config, NullLogger<DobEncryptionService>.Instance);
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    public static TheoryData<IAuthorizationHandler, IAuthorizationRequirement> SimpleHandlers => new()
    {
        { new SelfOnlyHandler(), new SelfOnlyRequirement() },
        { new ActiveAccountHandler(), new ActiveAccountRequirement() },
        { new NotLockedOutHandler(), new NotLockedOutRequirement() },
    };

    [Theory]
    [MemberData(nameof(SimpleHandlers))]
    public async Task SimpleHandler_AuthenticatedCaller_Succeeds(IAuthorizationHandler handler, IAuthorizationRequirement requirement)
    {
        var context = Context(requirement, Authenticated());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(SimpleHandlers))]
    public async Task SimpleHandler_AnonymousCaller_DoesNotSucceed(IAuthorizationHandler handler, IAuthorizationRequirement requirement)
    {
        var context = Context(requirement, Anonymous);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task AgeGate_AnonymousCaller_DoesNotSucceed()
    {
        var result = await AgeGateAsync(Anonymous);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AgeGate_SubjectIsNotAGuid_DoesNotSucceed()
    {
        var result = await AgeGateAsync(Authenticated(new Claim("sub", "not-a-guid")));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AgeGate_UnknownAccount_DoesNotSucceed()
    {
        var result = await AgeGateAsync(Authenticated(new Claim("sub", Guid.NewGuid().ToString())));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AgeGate_AccountWithoutDob_Succeeds()
    {
        var id = await SeedAccountAsync(dob: null);

        var result = await AgeGateAsync(Authenticated(new Claim("sub", id.ToString())));

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AgeGate_Adult_Succeeds()
    {
        var id = await SeedAccountAsync(_dob.Encrypt(new DateOnly(1990, 1, 1)));

        var result = await AgeGateAsync(Authenticated(new Claim(ClaimTypes.NameIdentifier, id.ToString())));

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AgeGate_Minor_DoesNotSucceed()
    {
        var tenYearsAgo = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10);
        var id = await SeedAccountAsync(_dob.Encrypt(tenYearsAgo));

        var result = await AgeGateAsync(Authenticated(new Claim("sub", id.ToString())));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AgeGate_CorruptCiphertext_DoesNotSucceed()
    {
        var id = await SeedAccountAsync(new byte[40]);

        var result = await AgeGateAsync(Authenticated(new Claim("sub", id.ToString())));

        result.Should().BeFalse();
    }

    private async Task<bool> AgeGateAsync(ClaimsPrincipal user)
    {
        var context = Context(new AgeGateRequirement(), user);
        await new AgeGateHandler(_services.GetRequiredService<IServiceScopeFactory>(), _dob).HandleAsync(context);
        return context.HasSucceeded;
    }

    private async Task<Guid> SeedAccountAsync(byte[]? dob)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var account = UserAccount.Create("EG", "en");
        if (dob is not null) account.SetDob(dob);
        db.UserAccounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    private static AuthorizationHandlerContext Context(IAuthorizationRequirement requirement, ClaimsPrincipal user) =>
        new([requirement], user, resource: null);
}
