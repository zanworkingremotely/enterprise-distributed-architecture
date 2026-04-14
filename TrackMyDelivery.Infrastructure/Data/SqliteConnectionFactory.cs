using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using TrackMyDelivery.Infrastructure.Configuration;

namespace TrackMyDelivery.Infrastructure.Data;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        var configuredPath = configuration[$"{StorageOptions.SectionName}:DatabasePath"]
            ?? "../TrackMyDelivery.SharedData/track-my-delivery.db";

        var databasePath = ResolvePath(configuredPath);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private static string ResolvePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
    }
}
