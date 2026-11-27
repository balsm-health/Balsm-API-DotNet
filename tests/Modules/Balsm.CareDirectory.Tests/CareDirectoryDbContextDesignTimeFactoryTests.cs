using Balsm.CareDirectory.Domain.Entities;
using Balsm.CareDirectory.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Xunit;

namespace Balsm.CareDirectory.Tests;

/// <summary>
/// The context <c>dotnet ef</c> builds at design time. The factory is internal
/// (only the EF tooling discovers it), so it is resolved by name. The provider
/// comes from <c>Database__Provider</c>, restored after every test; nothing
/// connects — building the model is all that is exercised.
/// </summary>
public sealed class CareDirectoryDbContextDesignTimeFactoryTests : IDisposable
{
    private readonly string? _originalProvider = Environment.GetEnvironmentVariable("Database__Provider");

    [Theory]
    [InlineData(null, "Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("sqlite", "Microsoft.EntityFrameworkCore.Sqlite")]
    public void CreateDbContext_ForProvider_BuildsTheCareDirectoryModel(string? provider, string expectedProvider)
    {
        Environment.SetEnvironmentVariable("Database__Provider", provider);
        var factory = (IDesignTimeDbContextFactory<CareDirectoryDbContext>)Activator.CreateInstance(
            typeof(CareDirectoryDbContext).Assembly.GetType(
                "Balsm.CareDirectory.Infrastructure.Configuration.CareDirectoryDbContextDesignTimeFactory", throwOnError: true)!,
            nonPublic: true)!;

        using var context = factory.CreateDbContext([]);

        context.Database.ProviderName.Should().Be(expectedProvider);
        context.Model.FindEntityType(typeof(CarePlace)).Should().NotBeNull();
    }

    public void Dispose() => Environment.SetEnvironmentVariable("Database__Provider", _originalProvider);
}
