using Balsm.Infrastructure.Lifecycle;
using FluentAssertions;
using Xunit;

namespace Balsm.API.Tests.Lifecycle;

public class DatabaseEndpointTests
{
    private const string Npgsql =
        "Host=localhost;Port=5433;Database=balsm_dev;Username=postgres;Password=hunter2";

    [Fact]
    public void Never_leaks_the_password() =>
        DatabaseEndpoint.Describe(Npgsql).Should().NotContain("hunter2");

    [Fact]
    public void Never_leaks_the_username() =>
        DatabaseEndpoint.Describe(Npgsql).Should().NotContain("postgres");

    [Fact]
    public void Describes_a_postgres_endpoint_as_host_port_database() =>
        DatabaseEndpoint.Describe(Npgsql).Should().Be("localhost:5433/balsm_dev");

    [Fact]
    public void Describes_sqlite_by_file_name_only() =>
        DatabaseEndpoint.Describe("Data Source=balsm_dev.db").Should().Be("balsm_dev.db");

    [Fact]
    public void Does_not_leak_the_directory_of_a_sqlite_path() =>
        DatabaseEndpoint.Describe("Data Source=/Users/someone/secret-project/balsm.db")
            .Should().Be("balsm.db");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Returns_null_rather_than_throwing_on_nothing_useful(string? connectionString) =>
        DatabaseEndpoint.Describe(connectionString).Should().BeNull();

    [Fact]
    public void Returns_null_when_there_is_no_host_to_report() =>
        DatabaseEndpoint.Describe("SomeOtherProvider=true;Foo=bar").Should().BeNull();
}
