namespace Balsam.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string Provider { get; set; } = "Sqlite";
    public string ConnectionString { get; set; } = "Data Source=balsam.db";
}
