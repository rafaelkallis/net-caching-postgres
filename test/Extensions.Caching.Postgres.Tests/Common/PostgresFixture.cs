using Npgsql;

using NpgsqlTypes;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests.Common;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string _adminConnectionString;
    private readonly string _testDatabase;
    public string ConnectionString { get; private set; }
    public string ConnectionStringPgBouncer { get; private set; }

    public PostgresFixture()
    {
        _adminConnectionString = "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=postgres;Include Error Detail=true;";
        _testDatabase = $"test_{DateTimeOffset.UtcNow:o}";
        ConnectionString = $"Host=127.0.0.1;Port=5432;Database={_testDatabase};Username=postgres;Password=postgres;Include Error Detail=true;";
        ConnectionStringPgBouncer = $"Host=127.0.0.1;Port=6432;Database={_testDatabase};Username=postgres;Password=postgres;No Reset On Close=true;Include Error Detail=true;";
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
        await Task.CompletedTask;
        // string sql = $@"DROP DATABASE ""{_testDatabase}"" (FORCE);";
        // await using NpgsqlConnection connection = new(_adminConnectionString);
        // await connection.OpenAsync();
        // await using NpgsqlCommand command = new(sql, connection);
        // await command.ExecuteNonQueryAsync(); 
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        NpgsqlConnection connection = new(ConnectionString);
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
        await using NpgsqlConnection connection = await OpenConnectionAsync();
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
        await using NpgsqlConnection connection = await OpenConnectionAsync();
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<CacheEntry?> SelectByKey(string key, string schema = PostgresCacheConstants.DefaultSchemaName, string table = PostgresCacheConstants.DefaultTableName)
    {
        string sql = $@"
            SELECT ""Key"", ""Value"", ""ExpiresAt"", ""SlidingExpiration"", ""AbsoluteExpiration""
            FROM ""{schema}"".""{table}""
            WHERE ""Key"" = $1;";
        await using NpgsqlConnection connection = await OpenConnectionAsync();
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
        await using NpgsqlConnection connection = await OpenConnectionAsync();
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
}