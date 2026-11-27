using Balsm.Deletion.Domain.Entities;
using Balsm.Deletion.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.Deletion.Tests;

/// <summary>
/// The context <c>dotnet ef</c> builds at design time. The provider comes from
/// <c>Database__Provider</c>, which is restored after every test. Nothing
/// connects: building the model is all that is exercised.
/// </summary>
public sealed class DeletionDbContextDesignTimeFactoryTests : IDisposable
{
    private readonly string? _originalProvider = Environment.GetEnvironmentVariable("Database__Provider");

    [Theory]
    [InlineData(null, "Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("postgresql", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("Sqlite", "Microsoft.EntityFrameworkCore.Sqlite")]
    public void CreateDbContext_ForProvider_BuildsTheDeletionModel(string? provider, string expectedProvider)
    {
        Environment.SetEnvironmentVariable("Database__Provider", provider);

        using var context = new DeletionDbContextDesignTimeFactory().CreateDbContext([]);

        context.Database.ProviderName.Should().Be(expectedProvider);
        context.Model.FindEntityType(typeof(DeletionLog)).Should().NotBeNull();
    }

    public void Dispose() => Environment.SetEnvironmentVariable("Database__Provider", _originalProvider);
}
