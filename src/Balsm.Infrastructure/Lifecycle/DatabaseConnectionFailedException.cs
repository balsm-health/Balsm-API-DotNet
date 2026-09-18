using System.Net.Sockets;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Balsm.Infrastructure.Lifecycle;

/// The database could not be reached at all — the server was not listening, the
/// host was unroutable, or the SQLite file could not be opened.
///
/// Separate from a plain <see cref="MigrationFailedException"/> because the fix
/// is completely different: nothing is wrong with the schema or the migration
/// code, something just is not running. Distinguishing the two is what turns
/// "migrations failed, see inner exception" into "start your database".
public sealed class DatabaseConnectionFailedException(string? endpoint, Exception inner)
    : MigrationFailedException(BuildMessage(endpoint), inner)
{
    /// Host:port/database, or the SQLite file name. Never contains credentials.
    public string? Endpoint { get; } = endpoint;

    private static string BuildMessage(string? endpoint) =>
        endpoint is null
            ? "Cannot reach the database. Check that it is running and that the connection string is correct."
            : $"Cannot reach the database at {endpoint}. Check that it is running and accepting connections.";

    /// True when <paramref name="exception"/>, or anything it wraps, is a failure
    /// to establish a connection rather than a failure to apply a migration.
    ///
    /// Matched on the transport outcome rather than on provider error codes: a
    /// refused/unreachable/timed-out socket means the same thing to the operator
    /// whatever the provider, and SQLite reports an unopenable file in place of
    /// a socket error because there is no socket.
    public static bool IsConnectionFailure(Exception? exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            switch (ex)
            {
                case SocketException socket when IsUnreachable(socket.SocketErrorCode):
                    return true;
                case SqliteException sqlite when sqlite.SqliteErrorCode == 14: // SQLITE_CANTOPEN
                    return true;
                case NpgsqlException { InnerException: null } npgsql
                    when npgsql.Message.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase):
                    return true;
                case TimeoutException:
                    return true;
            }
        }

        return false;
    }

    private static bool IsUnreachable(SocketError error) => error
        is SocketError.ConnectionRefused
        or SocketError.HostUnreachable
        or SocketError.NetworkUnreachable
        or SocketError.HostNotFound
        or SocketError.TimedOut
        or SocketError.TryAgain
        or SocketError.ConnectionReset
        or SocketError.ConnectionAborted;
}
