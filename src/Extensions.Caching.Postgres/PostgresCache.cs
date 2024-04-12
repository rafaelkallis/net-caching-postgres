using System.Data;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using NpgsqlTypes;

namespace RafaelKallis.Extensions.Caching.Postgres;

public sealed partial class PostgresCache : IDistributedCache
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

    private PostgresCacheOptions Options => _options.Value;

    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        using NpgsqlConnection connection = _npgsqlConnections.OpenConnection();
        using NpgsqlCommand command = new(_sqlQueries.GetCacheItem(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, Options.SystemClock.UtcNow.DateTime.ToUnixTimeMilliseconds());
        command.Prepare();
        using NpgsqlDataReader dataReader = command.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleRow | CommandBehavior.SingleResult);
        if (!dataReader.Read())
        {
            LogCacheItemNotFound(_logger, key);
            return null;
        }

        LogCacheItemFound(_logger, key);
        byte[] value = dataReader.GetFieldValue<byte[]>("Value");

        return value;
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string key, CancellationToken token)
    {
        await using NpgsqlConnection connection = await _npgsqlConnections.OpenConnectionAsync(token);
        await using NpgsqlCommand command = new(_sqlQueries.GetCacheItem(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, Options.SystemClock.UtcNow.DateTime.ToUnixTimeMilliseconds());
        await command.PrepareAsync(token);
        await using NpgsqlDataReader dataReader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleRow | CommandBehavior.SingleResult, token);
        if (!await dataReader.ReadAsync(token))
        {
            LogCacheItemNotFound(_logger, key);
            return null;
        }

        LogCacheItemFound(_logger, key);
        byte[] value = await dataReader.GetFieldValueAsync<byte[]>("Value", token);

        return value;
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
        using NpgsqlConnection connection = _npgsqlConnections.OpenConnection();
        using NpgsqlCommand command = new(_sqlQueries.RefreshCacheItem(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, Options.SystemClock.UtcNow.DateTime.ToUnixTimeMilliseconds());
        command.Prepare();
        command.ExecuteNonQuery();
        LogCacheItemRefreshed(_logger, key);
    }

    /// <inheritdoc />
    public async Task RefreshAsync(string key, CancellationToken token)
    {
        await using NpgsqlConnection connection = await _npgsqlConnections.OpenConnectionAsync(token);
        await using NpgsqlCommand command = new(_sqlQueries.RefreshCacheItem(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, Options.SystemClock.UtcNow.DateTime.ToUnixTimeMilliseconds());
        await command.PrepareAsync(token);
        await command.ExecuteNonQueryAsync(token);
        LogCacheItemRefreshed(_logger, key);
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        using NpgsqlConnection connection = _npgsqlConnections.OpenConnection();
        using NpgsqlCommand command = new(_sqlQueries.DeleteCacheItem(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Prepare();
        command.ExecuteNonQuery();
        LogCacheItemRemoved(_logger, key);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken token)
    {
        await using NpgsqlConnection connection = await _npgsqlConnections.OpenConnectionAsync(token);
        await using NpgsqlCommand command = new(_sqlQueries.DeleteCacheItem(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        await command.PrepareAsync(token);
        await command.ExecuteNonQueryAsync(token);
        LogCacheItemRemoved(_logger, key);
    }

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        DateTime now = Options.SystemClock.UtcNow.DateTime;
        DateTime? absoluteExpiration = ComputeAbsoluteExpiration(now, options);
        DateTime expiresAt = ComputeExpiresAt(now, absoluteExpiration, options);

        using NpgsqlConnection connection = _npgsqlConnections.OpenConnection();
        using NpgsqlCommand command = new(_sqlQueries.SetCacheItem(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bytea, value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, expiresAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, options.SlidingExpiration?.ToMilliseconds() as object
                                                             ?? DBNull.Value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, absoluteExpiration?.ToUnixTimeMilliseconds() as object
                                                             ?? DBNull.Value);
        command.Prepare();
        command.ExecuteNonQuery();
        LogCacheItemAdded(_logger, key);
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token)
    {
        DateTime now = Options.SystemClock.UtcNow.DateTime;
        DateTime? absoluteExpiration = ComputeAbsoluteExpiration(now, options);
        DateTime expiresAt = ComputeExpiresAt(now, absoluteExpiration, options);

        await using NpgsqlConnection connection = await _npgsqlConnections.OpenConnectionAsync(token);
        await using NpgsqlCommand command = new(_sqlQueries.SetCacheItem(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bytea, value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, expiresAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, options.SlidingExpiration?.ToMilliseconds() as object
                                                             ?? DBNull.Value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, absoluteExpiration?.ToUnixTimeMilliseconds() as object
                                                             ?? DBNull.Value);
        await command.PrepareAsync(token);
        await command.ExecuteNonQueryAsync(token);
        LogCacheItemAdded(_logger, key);
    }

    private static DateTime? ComputeAbsoluteExpiration(DateTime now, DistributedCacheEntryOptions options)
    {
        if (options.AbsoluteExpirationRelativeToNow is { } absoluteExpirationRelativeToNow)
        {
            return now.Add(absoluteExpirationRelativeToNow);
        }

        if (options.AbsoluteExpiration is { } absoluteExpiration)
        {
            if (absoluteExpiration.DateTime <= now)
            {
                throw new InvalidOperationException("The absolute expiration must be in the future.");
            }
            return absoluteExpiration.DateTime;
        }

        return null;
    }

    private DateTime ComputeExpiresAt(DateTime now, DateTime? absoluteExpiration, DistributedCacheEntryOptions options)
    {
        if (options.SlidingExpiration is { } slidingExpiration)
        {
            return now.Add(slidingExpiration);
        }

        if (absoluteExpiration is { } absoluteExpirationDateTime)
        {
            return absoluteExpirationDateTime;
        }

        return now.Add(Options.DefaultSlidingExpiration);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "The cache item with key {Key} was not found.")]
    private static partial void LogCacheItemNotFound(ILogger logger, string key);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "The cache item with key {Key} was found.")]
    private static partial void LogCacheItemFound(ILogger logger, string key);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "The cache item with key {Key} was removed.")]
    private static partial void LogCacheItemRemoved(ILogger logger, string key);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "The cache item with key {Key} was added.")]
    private static partial void LogCacheItemAdded(ILogger logger, string key);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "The cache item with key {Key} was refreshed.")]
    private static partial void LogCacheItemRefreshed(ILogger logger, string key);
}