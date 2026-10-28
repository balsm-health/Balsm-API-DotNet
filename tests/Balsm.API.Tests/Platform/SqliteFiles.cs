using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Balsm.API.Tests.Platform;

/// <summary>Throwaway on-disk SQLite files for the backup and restore tests.</summary>
internal static class SqliteFiles
{
    public static void Create(string path)
    {
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE sample (id INTEGER PRIMARY KEY, name TEXT); INSERT INTO sample (name) VALUES ('row');";
            command.ExecuteNonQuery();
        }
    }

    public static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    public static void DeleteDirectory(string path)
    {
        // Pooled connections opened by the code under test keep their files open.
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }
}
