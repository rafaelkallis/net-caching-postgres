using System.Data;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using NpgsqlTypes;

namespace RafaelKallis.Extensions.Caching.Postgres;

public sealed class PostgresCache : IDistributedCache
{
    private readonly ILogger<PostgresCache> _logger;
    private readonly IOptions<PostgresCacheOptions> _options;
    private readonly NpgsqlConnections _npgsqlConnections;
    private readonly SqlQueries _sqlQueries;

    public PostgresCache(ILogger<PostgresCache> logger, IOptions<PostgresCacheOptions> options, NpgsqlConnections npgsqlConnections, SqlQueries sqlQueries)
    {
        _logger = logger;
        _options = options;
        _npgsqlConnections = npgsqlConnections;
        _sqlQueries = sqlQueries;
    }

    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        using NpgsqlConnection connection = _npgsqlConnections.OpenConnection();
        using NpgsqlCommand command = new(_sqlQueries.GetCacheItem(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, DateTimeOffset.UtcNow);
        command.Prepare();
        using NpgsqlDataReader dataReader = command.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleRow | CommandBehavior.SingleResult);
        if (!dataReader.Read())
        {
            return null;
        }

        byte[] value = dataReader.GetFieldValue<byte[]>("Value");

        return value;
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string key, CancellationToken token)
    {
        await using NpgsqlConnection connection = await _npgsqlConnections.OpenConnectionAsync(token);
        await using NpgsqlCommand command = new(_sqlQueries.GetCacheItem(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, DateTimeOffset.UtcNow);
        await command.PrepareAsync(token);
        await using NpgsqlDataReader dataReader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleRow | CommandBehavior.SingleResult, token);
        if (!await dataReader.ReadAsync(token))
        {
            return null;
        }

        byte[] value = await dataReader.GetFieldValueAsync<byte[]>("Value", token);

        return value;
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task RefreshAsync(string key, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token)
    {
        throw new NotImplementedException();
    }
}