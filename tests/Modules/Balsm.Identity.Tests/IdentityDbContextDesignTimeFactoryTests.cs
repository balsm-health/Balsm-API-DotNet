using Balsm.Identity.Domain;
using Balsm.Identity.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Xunit;

namespace Balsm.Identity.Tests;

/// <summary>
/// The context <c>dotnet ef</c> builds at design time. The factory is internal
/// (only the EF tooling discovers it), so it is resolved by name. The provider
/// comes from <c>Database__Provider</c>, restored after every test; nothing
/// connects — building the model is all that is exercised.
/// </summary>
public sealed class IdentityDbContextDesignTimeFactoryTests : IDisposable
{
    private readonly string? _originalProvider = Environment.GetEnvironmentVariable("Database__Provider");

    [Theory]
    [InlineData(null, "Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("sqlite", "Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("PostgreSQL", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void CreateDbContext_ForProvider_BuildsTheIdentityModel(string? provider, string expectedProvider)
    {
        Environment.SetEnvironmentVariable("Database__Provider", provider);
        var factory = (IDesignTimeDbContextFactory<IdentityDbContext>)Activator.CreateInstance(
            typeof(IdentityDbContext).Assembly.GetType(
                "Balsm.Identity.Infrastructure.Configuration.IdentityDbContextDesignTimeFactory", throwOnError: true)!,
            nonPublic: true)!;

        using var context = factory.CreateDbContext([]);

        context.Database.ProviderName.Should().Be(expectedProvider);
        context.Model.FindEntityType(typeof(AdminUserMirror)).Should().NotBeNull();
    }

    public void Dispose() => Environment.SetEnvironmentVariable("Database__Provider", _originalProvider);
}
