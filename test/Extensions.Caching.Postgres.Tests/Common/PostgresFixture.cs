using Npgsql;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests.Common;

[CollectionDefinition(PostgresFixture.CollectionName)]
public class PostgresCollectionFixture : ICollectionFixture<PostgresFixture>;

public sealed class PostgresFixture : IAsyncLifetime
{
    public const string CollectionName = "Postgres";

    private const string DefaultHost = "127.0.0.1";
    private const string DefaultDatabase = "postgres";
    private const string DefaultUsername = "postgres";
    private const string DefaultPassword = "pAssw0rd";

    public string ConnectionString { get; }
    private readonly string _database;
#if NET8_0_OR_GREATER
    private readonly NpgsqlDataSource _dataSource;
#endif

    public async Task<NpgsqlConnection> OpenConnection()
    {
#if NET8_0_OR_GREATER
        NpgsqlConnection connection = _dataSource.CreateConnection();  
#else
        NpgsqlConnection connection = new(ConnectionString);
#endif
        await connection.OpenAsync();
        return connection;
    }

    public PostgresFixture()
    {
        _database = $"test_{DateTime.UtcNow:o}";
        ConnectionString = CreateConnectionString(database: _database);
#if NET8_0_OR_GREATER
        _dataSource = NpgsqlDataSource.Create(ConnectionString);
#endif
    }

    public async Task InitializeAsync()
    {
        await using NpgsqlConnection connection = new(CreateConnectionString());
        await connection.OpenAsync();
        string sql = $@"CREATE DATABASE ""{_database}"";";
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
#if NET8_0_OR_GREATER
        await _dataSource.DisposeAsync();
#endif
        string sql = $@"DROP DATABASE ""{_database}"" (FORCE);";
        await using NpgsqlConnection connection = new(CreateConnectionString());
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string CreateConnectionString(string host = DefaultHost, string database = DefaultDatabase, string username = DefaultUsername, string password = DefaultPassword) =>
        $"Host={host};Database={database};Username={username};Password={password};Include Error Detail=true;Maximum Pool Size=20";
}