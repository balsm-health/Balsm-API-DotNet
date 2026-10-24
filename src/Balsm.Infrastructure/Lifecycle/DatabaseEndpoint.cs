using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Infrastructure.Lifecycle;

/// Renders a connection string as something safe to log.
///
/// Connection strings carry credentials, so nothing here ever returns the raw
/// string: only the host/port (or the SQLite file name) a human needs in order
/// to work out which database is unreachable.
public static class DatabaseEndpoint
{
    /// "localhost:5433/balsm_dev", "balsm_dev.db", or null when it cannot be read.
    public static string? Describe(DbContext context)
    {
        try
        {
            return Describe(context.Database.GetConnectionString());
        }
        catch
        {
            // Never let diagnostics be the thing that breaks startup.
            return null;
        }
    }

    public static string? Describe(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;

        DbConnectionStringBuilder builder;
        try
        {
            builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (Value(builder, "Data Source") is { Length: > 0 } file && !file.Contains('='))
        {
            // Sqlite. Path only — it can be absolute and reveal a home directory,
            // so keep the file name.
            return Path.GetFileName(file);
        }

        var host = Value(builder, "Host") ?? Value(builder, "Server");
        if (host is null) return null;

        var port = Value(builder, "Port");
        var database = Value(builder, "Database");

        var endpoint = port is null ? host : $"{host}:{port}";
        return database is null ? endpoint : $"{endpoint}/{database}";
    }

    private static string? Value(DbConnectionStringBuilder builder, string key) =>
        builder.TryGetValue(key, out var v) && v?.ToString() is { Length: > 0 } s ? s : null;
}
