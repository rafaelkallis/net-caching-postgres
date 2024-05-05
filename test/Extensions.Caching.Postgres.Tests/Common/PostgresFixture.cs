using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;

using Npgsql;

using NpgsqlTypes;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests.Common;

public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultHost = "127.0.0.1";
    private const string DefaultDatabase = "postgres";
    private const string DefaultUsername = "postgres";
    private const string DefaultPassword = "pAssw0rd";

    private readonly string _adminConnectionString;
    private readonly string _testDatabase;
    private readonly string _testConnectionString;
    private readonly NpgsqlDataSource _dataSource;

    public string ConnectionString => _testConnectionString;

    public PostgresFixture()
    {
        _adminConnectionString = CreateConnectionString();
        _testDatabase = $"test_{DateTimeOffset.UtcNow:o}_{Guid.NewGuid():N}";
        _testConnectionString = CreateConnectionString(database: _testDatabase);
        _dataSource = NpgsqlDataSource.Create(ConnectionString);
    }

    public async Task<NpgsqlConnection> OpenConnection()
    {
        NpgsqlConnection connection = _dataSource.CreateConnection();
        await connection.OpenAsync();
        return connection;
    }

    public async Task Insert(CacheEntry cacheEntry, string schema = PostgresCacheConstants.DefaultSchemaName, string table = PostgresCacheConstants.DefaultTableName)
    {
        ArgumentNullException.ThrowIfNull(cacheEntry);
        string sql = $@"
            INSERT INTO ""{schema}"".""{table}""
            (""Key"", ""Value"", ""ExpiresAt"", ""SlidingExpiration"", ""AbsoluteExpiration"")
            VALUES ($1, $2, $3, $4, $5);";
        await using NpgsqlConnection connection = await OpenConnection();
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, cacheEntry.Key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bytea, cacheEntry.Value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, cacheEntry.ExpiresAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue<long?>(NpgsqlDbType.Bigint, cacheEntry.SlidingExpiration?.ToMilliseconds());
        command.Parameters.AddWithValue<long?>(NpgsqlDbType.Bigint, cacheEntry.AbsoluteExpiration?.ToUnixTimeMilliseconds());
        await command.PrepareAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task Truncate(string schema = PostgresCacheConstants.DefaultSchemaName, string table = PostgresCacheConstants.DefaultTableName)
    {
        string sql = $@"TRUNCATE TABLE ""{schema}"".""{table}""";
        await using NpgsqlConnection connection = await OpenConnection();
        await using NpgsqlCommand command = new(sql, connection);
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
            Value: await dataReader.GetFieldValueAsync<byte[]>(1),
            ExpiresAt: dataReader.GetInt64(2).AsUnixTimeMillisecondsDateTime(),
            SlidingExpiration: !await dataReader.IsDBNullAsync(3)
                ? dataReader.GetInt64(3).AsMillisecondsTimeSpan()
                : null,
            AbsoluteExpiration: !await dataReader.IsDBNullAsync(4)
                ? dataReader.GetInt64(4).AsUnixTimeMillisecondsDateTime()
                : null);
    }

    public async IAsyncEnumerable<CacheEntry> SelectByKeyPattern(string keyPattern, string schema = PostgresCacheConstants.DefaultSchemaName, string table = PostgresCacheConstants.DefaultTableName)
    {
        string sql = $@"
            SELECT ""Key"", ""Value"", ""ExpiresAt"", ""SlidingExpiration"", ""AbsoluteExpiration""
            FROM ""{schema}"".""{table}""
            WHERE ""Key"" LIKE $1;";
        await using NpgsqlConnection connection = await OpenConnection();
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, keyPattern);
        await command.PrepareAsync();
        await using NpgsqlDataReader dataReader = await command.ExecuteReaderAsync();
        while (await dataReader.ReadAsync())
        {
            yield return new CacheEntry(
                Key: dataReader.GetString(0),
                Value: await dataReader.GetFieldValueAsync<byte[]>(1),
                ExpiresAt: dataReader.GetInt64(2).AsUnixTimeMillisecondsDateTime(),
                SlidingExpiration: !await dataReader.IsDBNullAsync(3)
                    ? dataReader.GetInt64(3).AsMillisecondsTimeSpan()
                    : null,
                AbsoluteExpiration: !await dataReader.IsDBNullAsync(4)
                    ? dataReader.GetInt64(4).AsUnixTimeMillisecondsDateTime()
                    : null);
        }
    }

    public async Task InitializeAsync()
    {
        await using NpgsqlConnection connection = new(_adminConnectionString);
        await connection.OpenAsync();
        string sql = $@"CREATE DATABASE ""{_testDatabase}"";";
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        string sql = $@"DROP DATABASE ""{_testDatabase}"" (FORCE);";
        await using NpgsqlConnection connection = new(_adminConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string CreateConnectionString(string host = DefaultHost, string database = DefaultDatabase, string username = DefaultUsername, string password = DefaultPassword) =>
        $"Host={host};Database={database};Username={username};Password={password};Include Error Detail=true;";
}