using System.Data;
using System.Diagnostics;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using NpgsqlTypes;

namespace RafaelKallis.Extensions.Caching.Postgres;

/// <summary>
/// Postgres based <see cref="IDistributedCache"/>.
/// </summary>
public sealed partial class PostgresCache(
    ILogger<PostgresCache> logger,
    IOptions<PostgresCacheOptions> postgresCacheOptions,
    NpgsqlConnections npgsqlConnections,
    SqlQueries sqlQueries,
    TimeProvider timeProvider)
    : IDistributedCache
{
    private const string SqlStateTransactionRollbackPrefix = "40";

    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        using NpgsqlConnection connection = npgsqlConnections.OpenConnection();
        using NpgsqlCommand command = new(sqlQueries.GetCacheEntry(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
        command.Prepare();
        using NpgsqlDataReader dataReader = command.ExecuteReader(CommandBehavior.SingleRow | CommandBehavior.SingleResult);
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
        await using NpgsqlDataReader dataReader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow | CommandBehavior.SingleResult, token);
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
        ValidateOptions(options, now);
        TimeSpan? slidingExpiration = GetSlidingExpiration(options);
        DateTimeOffset? absoluteExpiration = GetAbsoluteExpiration(now, options);
        DateTimeOffset expiresAt = ComputeExpiresAt(now, absoluteExpiration, options);
        Debug.Assert(slidingExpiration is not null || absoluteExpiration is not null);

        using NpgsqlConnection connection = npgsqlConnections.OpenConnection();
        using NpgsqlCommand command = new(sqlQueries.SetCacheEntry(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bytea, value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, expiresAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, slidingExpiration?.ToMilliseconds() as object
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
        ValidateOptions(options, now);
        TimeSpan? slidingExpiration = GetSlidingExpiration(options);
        DateTimeOffset? absoluteExpiration = GetAbsoluteExpiration(now, options);
        DateTimeOffset expiresAt = ComputeExpiresAt(now, absoluteExpiration, options);
        Debug.Assert(slidingExpiration is not null || absoluteExpiration is not null);

        await using NpgsqlConnection connection = await npgsqlConnections.OpenConnectionAsync(token);
        await using NpgsqlCommand command = new(sqlQueries.SetCacheEntry(), connection);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
        command.Parameters.AddWithValue(NpgsqlDbType.Bytea, value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, expiresAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, slidingExpiration?.ToMilliseconds() as object
                                                             ?? DBNull.Value);
        command.Parameters.AddWithValue(NpgsqlDbType.Bigint, absoluteExpiration?.ToUnixTimeMilliseconds() as object
                                                             ?? DBNull.Value);
        await command.PrepareAsync(token);
        await command.ExecuteNonQueryAsync(token);
        LogCacheEntryAdded(logger, key);
    }

    /// <summary>
    /// Removes all expired cache entries.
    /// </summary>
    /// <remarks>
    /// If an error occurs during garbage collection,
    /// the method will retry once with a lock and ultimately with truncation.
    /// </remarks>
    public async Task RunGarbageCollection(CancellationToken ct = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        LogDeletingExpiredCacheEntries(logger);
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
        catch (PostgresException e) when (e.SqlState.StartsWith(SqlStateTransactionRollbackPrefix, StringComparison.Ordinal))
        {
            // can happen
            logger.LogWarning(e, "Transaction rollback detected \"{SqlState}\" during garbage collection", e.SqlState);
        }
        catch (PostgresException e)
        {
            logger.LogError(e, "Postgres error \"{SqlState}\" during garbage collection", e.SqlState);
        }
        catch (Exception e)
        {
            logger.LogError("An error occurred during garbage collection: {Message}", e.Message);
        }

        logger.LogDebug("Retry with lock");

        try
        {
            await using NpgsqlCommand command = new(sqlQueries.DeleteExpiredCacheEntriesWithLock(), connection);
            command.Parameters.AddWithValue(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
            await command.PrepareAsync(ct);
            int rows = await command.ExecuteNonQueryAsync(ct);
            LogDeletedExpiredCacheEntries(logger, rows);
            return;
        }
        catch (PostgresException e)
        {
            logger.LogError(e, "Postgres error \"{SqlState}\" during garbage collection", e.SqlState);
        }
        catch (Exception e)
        {
            logger.LogError("An error occurred during garbage collection: {Message}", e.Message);
        }

        logger.LogWarning("Final retry with truncate");

        try
        {
            await using NpgsqlCommand command = new(sqlQueries.TruncateCacheEntries(), connection);
            await command.PrepareAsync(ct);
            int rows = await command.ExecuteNonQueryAsync(ct);
            LogDeletedExpiredCacheEntries(logger, rows);
            return;
        }
        catch (PostgresException e)
        {
            logger.LogError(e, "Postgres error \"{SqlState}\" during garbage collection", e.SqlState);
        }
        catch (Exception e)
        {
            logger.LogError("An error occurred during garbage collection: {Message}", e.Message);
        }
    }

    /// <summary>
    /// Performs the migration of the database.
    /// </summary>
    public async Task MigrateAsync(CancellationToken ct)
    {
        await using NpgsqlConnection connection = await npgsqlConnections.OpenConnectionAsync(ct);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        string sql = sqlQueries.Migration();
        await using NpgsqlCommand command = new(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        LogDatabaseMigrated(logger);
    }

    private static void ValidateOptions(DistributedCacheEntryOptions options, DateTimeOffset now)
    {
        if (options.SlidingExpiration is { } slidingExpiration && slidingExpiration < TimeSpan.Zero)
        {
            throw new InvalidOperationException("The sliding expiration must be positive.");
        }
        if (options.AbsoluteExpirationRelativeToNow is { } absoluteExpirationRelativeToNow && absoluteExpirationRelativeToNow < TimeSpan.Zero)
        {
            throw new InvalidOperationException("The absolute expiration relative to now must be positive.");
        }
        if (options.AbsoluteExpiration is { } absoluteExpiration && absoluteExpiration <= now)
        {
            throw new InvalidOperationException("The absolute expiration must be in the future.");
        }
    }

    private TimeSpan? GetSlidingExpiration(DistributedCacheEntryOptions options)
    {
        if (options.SlidingExpiration is { } slidingExpiration)
        {
            return slidingExpiration;
        }
        if (options is { AbsoluteExpiration: null, AbsoluteExpirationRelativeToNow: null })
        {
            return postgresCacheOptions.Value.DefaultSlidingExpiration;
        }
        return null;
    }

    private static DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset now, DistributedCacheEntryOptions options)
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

        return now + postgresCacheOptions.Value.DefaultSlidingExpiration;
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

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "Deleting expired cache entries")]
    private static partial void LogDeletingExpiredCacheEntries(ILogger logger);

    [LoggerMessage(EventId = 7, Level = LogLevel.Debug, Message = "Deleted {Count} expired cache entries")]
    private static partial void LogDeletedExpiredCacheEntries(ILogger logger, int count);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "Database migrated")]
    private static partial void LogDatabaseMigrated(ILogger logger);
}