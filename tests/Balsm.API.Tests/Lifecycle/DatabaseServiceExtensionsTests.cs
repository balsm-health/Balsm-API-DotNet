using Balsm.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace Balsm.API.Tests.Lifecycle;

public sealed class DatabaseServiceExtensionsTests
{
    [Fact]
    public void ConfigureDatabase_PostgreSql_UsesNpgsqlWithTheGivenMigrationsAssembly()
    {
        var builder = new DbContextOptionsBuilder();

        builder.ConfigureDatabase(
            new DatabaseOptions { Provider = "PostgreSql", ConnectionString = "Host=localhost;Database=balsm_test" },
            npgsqlMigrationsAssembly: "Balsm.Infrastructure.Migrations.Npgsql");

        builder.Options.Extensions.OfType<RelationalOptionsExtension>().Single().MigrationsAssembly
            .Should().Be("Balsm.Infrastructure.Migrations.Npgsql");
    }

    [Fact]
    public void ConfigureDatabase_Sqlite_UsesTheGivenMigrationsAssembly()
    {
        var builder = new DbContextOptionsBuilder();

        builder.ConfigureDatabase(
            new DatabaseOptions { Provider = "Sqlite", ConnectionString = "Data Source=:memory:" },
            sqliteMigrationsAssembly: "Balsm.Sample.Migrations.Sqlite");

        builder.Options.Extensions.OfType<RelationalOptionsExtension>().Single().MigrationsAssembly
            .Should().Be("Balsm.Sample.Migrations.Sqlite");
    }

    [Fact]
    public void ConfigureDatabase_UnknownProvider_Throws()
    {
        var builder = new DbContextOptionsBuilder();

        var act = () => builder.ConfigureDatabase(new DatabaseOptions { Provider = "Oracle", ConnectionString = "x" });

        act.Should().Throw<InvalidOperationException>().WithMessage("*Oracle*");
    }
}
