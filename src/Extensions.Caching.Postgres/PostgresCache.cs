using System.Data;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using NpgsqlTypes;

namespace RafaelKallis.Extensions.Caching.Postgres;

public sealed partial class PostgresCache(
    ILogger<PostgresCache> logger,
    IOptionsMonitor<PostgresCacheOptions> postgresCacheOptions,
    NpgsqlConnections npgsqlConnections,
    SqlQueries sqlQueries,
    TimeProvider timeProvider)
    : IDistributedCache
{
    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        using NpgsqlConnection connection = npgsqlConnections.OpenConnection();
        using NpgsqlCommand command = new(sqlQueries.GetCacheEntry(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
        command.Prepare();
        using NpgsqlDataReader dataReader = command.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleRow | CommandBehavior.SingleResult);
        if (!dataReader.Read())
        {
            LogCacheEntryNotFound(logger, key);
            return null;
        }

        LogCacheEntryFound(logger, key);
        byte[] value = dataReader.GetFieldValue<byte[]>("Value");

        return value;
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string key, CancellationToken token)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await using NpgsqlConnection connection = await npgsqlConnections.OpenConnectionAsync(token);
        await using NpgsqlCommand command = new(sqlQueries.GetCacheEntry(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
        await command.PrepareAsync(token);
        await using NpgsqlDataReader dataReader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleRow | CommandBehavior.SingleResult, token);
        if (!await dataReader.ReadAsync(token))
        {
            LogCacheEntryNotFound(logger, key);
            return null;
        }

        LogCacheEntryFound(logger, key);
        byte[] value = await dataReader.GetFieldValueAsync<byte[]>("Value", token);

        return value;
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        using NpgsqlConnection connection = npgsqlConnections.OpenConnection();
        using NpgsqlCommand command = new(sqlQueries.RefreshCacheEntry(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
        command.Prepare();
        command.ExecuteNonQuery();
        LogCacheEntryRefreshed(logger, key);
    }

    /// <inheritdoc />
    public async Task RefreshAsync(string key, CancellationToken token)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await using NpgsqlConnection connection = await npgsqlConnections.OpenConnectionAsync(token);
        await using NpgsqlCommand command = new(sqlQueries.RefreshCacheEntry(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
        await command.PrepareAsync(token);
        await command.ExecuteNonQueryAsync(token);
        LogCacheEntryRefreshed(logger, key);
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        using NpgsqlConnection connection = npgsqlConnections.OpenConnection();
        using NpgsqlCommand command = new(sqlQueries.DeleteCacheEntry(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Prepare();
        command.ExecuteNonQuery();
        LogCacheEntryRemoved(logger, key);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken token)
    {
        await using NpgsqlConnection connection = await npgsqlConnections.OpenConnectionAsync(token);
        await using NpgsqlCommand command = new(sqlQueries.DeleteCacheEntry(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        await command.PrepareAsync(token);
        await command.ExecuteNonQueryAsync(token);
        LogCacheEntryRemoved(logger, key);
    }

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset? absoluteExpiration = ComputeAbsoluteExpiration(now, options);
        DateTimeOffset expiresAt = ComputeExpiresAt(now, absoluteExpiration, options);

        using NpgsqlConnection connection = npgsqlConnections.OpenConnection();
        using NpgsqlCommand command = new(sqlQueries.SetCacheEntry(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bytea, value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, expiresAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, options.SlidingExpiration?.ToMilliseconds() as object
                                                             ?? DBNull.Value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, absoluteExpiration?.ToUnixTimeMilliseconds() as object
                                                             ?? DBNull.Value);
        command.Prepare();
        command.ExecuteNonQuery();
        LogCacheEntryAdded(logger, key);
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset? absoluteExpiration = ComputeAbsoluteExpiration(now, options);
        DateTimeOffset expiresAt = ComputeExpiresAt(now, absoluteExpiration, options);

        await using NpgsqlConnection connection = await npgsqlConnections.OpenConnectionAsync(token);
        await using NpgsqlCommand command = new(sqlQueries.SetCacheEntry(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bytea, value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, expiresAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, options.SlidingExpiration?.ToMilliseconds() as object
                                                             ?? DBNull.Value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, absoluteExpiration?.ToUnixTimeMilliseconds() as object
                                                             ?? DBNull.Value);
        await command.PrepareAsync(token);
        await command.ExecuteNonQueryAsync(token);
        LogCacheEntryAdded(logger, key);
    }

    public async Task RunGarbageCollection(CancellationToken ct = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await using NpgsqlConnection connection = await npgsqlConnections.OpenConnectionAsync(ct);

        try
        {
            await using NpgsqlCommand command = new(sqlQueries.DeleteExpiredCacheEntries(), connection);
            command.Parameters.AddWithValue(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
            await command.PrepareAsync(ct);
            int rows = await command.ExecuteNonQueryAsync(ct);
            LogDeletedExpiredCacheEntries(logger, rows);
            return;
        }
        catch (PostgresException e) when (e.SqlState == "40P01")
        {
            logger.LogWarning(e, "Deadlock detected during garbage collection, retrying with a lock");
        }

        await using NpgsqlCommand command2 = new(sqlQueries.DeleteExpiredCacheEntriesWithLock(), connection);
        command2.Parameters.AddWithValue(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
        await command2.PrepareAsync(ct);
        int rows2 = await command2.ExecuteNonQueryAsync(ct);
        LogDeletedExpiredCacheEntries(logger, rows2);
    }

    private static DateTimeOffset? ComputeAbsoluteExpiration(DateTimeOffset now, DistributedCacheEntryOptions options)
    {
        if (options.AbsoluteExpirationRelativeToNow is { } absoluteExpirationRelativeToNow)
        {
            return now + absoluteExpirationRelativeToNow;
        }

        if (options.AbsoluteExpiration is { } absoluteExpiration)
        {
            if (absoluteExpiration <= now)
            {
                throw new InvalidOperationException("The absolute expiration must be in the future.");
            }
            return absoluteExpiration;
        }

        return null;
    }

    private DateTimeOffset ComputeExpiresAt(DateTimeOffset now, DateTimeOffset? absoluteExpiration, DistributedCacheEntryOptions options)
    {
        if (options.SlidingExpiration is { } slidingExpiration)
        {
            return now + slidingExpiration;
        }

        if (absoluteExpiration is { } absoluteExpirationDateTime)
        {
            return absoluteExpirationDateTime;
        }

        return now + postgresCacheOptions.CurrentValue.DefaultSlidingExpiration;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "The cache entry with key {Key} was not found.")]
    private static partial void LogCacheEntryNotFound(ILogger logger, string key);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "The cache entry with key {Key} was found.")]
    private static partial void LogCacheEntryFound(ILogger logger, string key);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "The cache entry with key {Key} was removed.")]
    private static partial void LogCacheEntryRemoved(ILogger logger, string key);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "The cache entry with key {Key} was added.")]
    private static partial void LogCacheEntryAdded(ILogger logger, string key);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "The cache entry with key {Key} was refreshed.")]
    private static partial void LogCacheEntryRefreshed(ILogger logger, string key);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "Deleted {Count} expired cache entries")]
    private static partial void LogDeletedExpiredCacheEntries(ILogger logger, int count);
}