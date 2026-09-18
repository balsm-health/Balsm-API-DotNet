using System.Net.Sockets;
using Balsm.Infrastructure.Lifecycle;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Balsm.API.Tests.Lifecycle;

public class DatabaseConnectionFailureTests
{
    [Theory]
    [InlineData(SocketError.ConnectionRefused)]
    [InlineData(SocketError.HostUnreachable)]
    [InlineData(SocketError.NetworkUnreachable)]
    [InlineData(SocketError.HostNotFound)]
    [InlineData(SocketError.TimedOut)]
    public void Classifies_unreachable_sockets_as_connection_failures(SocketError error) =>
        DatabaseConnectionFailedException.IsConnectionFailure(new SocketException((int)error))
            .Should().BeTrue();

    [Fact]
    public void Finds_the_socket_failure_however_deeply_it_is_wrapped()
    {
        // The real shape: MigrationRunner sees an EF wrapper around an Npgsql
        // wrapper around the socket error.
        var actual = new InvalidOperationException(
            "An exception occurred while iterating over the results",
            new Exception(
                "Failed to connect to 127.0.0.1:5433",
                new SocketException((int)SocketError.ConnectionRefused)));

        DatabaseConnectionFailedException.IsConnectionFailure(actual).Should().BeTrue();
    }

    [Fact]
    public void Treats_an_unopenable_sqlite_file_as_a_connection_failure() =>
        DatabaseConnectionFailureTestsHelpers.CantOpenSqlite()
            .Let(DatabaseConnectionFailedException.IsConnectionFailure)
            .Should().BeTrue();

    [Fact]
    public void Does_not_classify_a_genuine_migration_error_as_a_connection_failure()
    {
        // A broken migration must stay a MigrationFailedException — telling the
        // operator to "start the database" here would send them the wrong way.
        var brokenMigration = new InvalidOperationException(
            """relation "health_record" already exists""");

        DatabaseConnectionFailedException.IsConnectionFailure(brokenMigration).Should().BeFalse();
    }

    [Fact]
    public void Is_a_MigrationFailedException_so_the_host_catch_still_sees_it() =>
        new DatabaseConnectionFailedException("localhost:5433/balsm_dev", new Exception())
            .Should().BeAssignableTo<MigrationFailedException>();

    [Fact]
    public void Names_the_endpoint_in_the_message() =>
        new DatabaseConnectionFailedException("localhost:5433/balsm_dev", new Exception())
            .Message.Should().Contain("localhost:5433/balsm_dev");

    [Fact]
    public void Still_reads_sensibly_when_the_endpoint_is_unknown() =>
        new DatabaseConnectionFailedException(null, new Exception())
            .Message.Should().Contain("Cannot reach the database");
}

file static class DatabaseConnectionFailureTestsHelpers
{
    public static SqliteException CantOpenSqlite() => new("unable to open database file", 14, 14);

    public static TResult Let<T, TResult>(this T value, Func<T, TResult> f) => f(value);
}
