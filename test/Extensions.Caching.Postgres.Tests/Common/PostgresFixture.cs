using Npgsql;

using NpgsqlTypes;

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
#if NET7_0_OR_GREATER
    private readonly NpgsqlDataSource _dataSource;
#endif

    public PostgresFixture()
    {
        _database = $"test_{DateTime.UtcNow:o}";
        ConnectionString = CreateConnectionString(database: _database);
#if NET7_0_OR_GREATER
        _dataSource = NpgsqlDataSource.Create(ConnectionString);
#endif
    }

    public async Task<NpgsqlConnection> OpenConnection()
    {
#if NET7_0_OR_GREATER
        NpgsqlConnection connection = _dataSource.CreateConnection();  
#else
        NpgsqlConnection connection = new(ConnectionString);
#endif
        await connection.OpenAsync();
        return connection;
    }

    public async Task Insert(CacheEntry cacheEntry, string schema = PostgresCacheConstants.DefaultSchemaName, string table = PostgresCacheConstants.DefaultTableName)
    {
        string sql = $@"
            INSERT INTO ""{schema}"".""{table}""
            (""Key"", ""Value"", ""ExpiresAt"", ""SlidingExpiration"", ""AbsoluteExpiration"")
            VALUES ($1, $2, $3, $4, $5);";
        await using NpgsqlConnection connection = await OpenConnection();
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, cacheEntry.Key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bytea, cacheEntry.Value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, cacheEntry.ExpiresAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, cacheEntry.SlidingExpiration?.ToMilliseconds() as object ?? DBNull.Value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, cacheEntry.AbsoluteExpiration?.ToUnixTimeMilliseconds() as object ?? DBNull.Value);
        await command.PrepareAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task<CacheEntry?> SelectByKey(string key, string schema = PostgresCacheConstants.DefaultSchemaName, string table = PostgresCacheConstants.DefaultTableName)
    {
        string sql = $@"
            SELECT ""Key"", ""Value"", ""ExpiresAt"", ""SlidingExpiration"", ""AbsoluteExpiration""
            FROM ""{schema}"".""{table}""
            WHERE ""Key"" = $1;";
        await using NpgsqlConnection connection = await OpenConnection();
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        await command.PrepareAsync();
        await using NpgsqlDataReader dataReader = await command.ExecuteReaderAsync();
        if (!await dataReader.ReadAsync())
        {
            return null;
        }

        return new CacheEntry(
            Key: dataReader.GetString(0),
            Value: dataReader.GetFieldValue<byte[]>(1),
            ExpiresAt: dataReader.GetInt64(2).AsUnixTimeMillisecondsDateTime(),
            SlidingExpiration: !dataReader.IsDBNull(3)
                ? dataReader.GetInt64(3).AsMillisecondsTimeSpan()
                : null,
            AbsoluteExpiration: !dataReader.IsDBNull(4)
                ? dataReader.GetInt64(4).AsUnixTimeMillisecondsDateTime()
                : null);
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
#if NET7_0_OR_GREATER
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