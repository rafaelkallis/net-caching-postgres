using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

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
    PostgresCacheMetrics metrics,
    IOptions<PostgresCacheOptions> postgresCacheOptions,
    ConnectionFactory connectionFactory,
    SqlQueries sqlQueries,
    TimeProvider timeProvider)
    : IDistributedCache
{
    private const string SqlStateTransactionRollbackPrefix = "40";

    private PostgresCacheOptions Options => postgresCacheOptions.Value;

    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        using Activity? activity = PostgresCacheActivitySource.StartGetActivity(key);
        Stopwatch stopwatch = Stopwatch.StartNew();
        DateTimeOffset now = timeProvider.GetUtcNow();
        using NpgsqlConnection connection = connectionFactory.OpenConnection();
        string sql = sqlQueries.GetCacheEntry;
        if (!Options.UsePreparedStatements)
        {
            sql = sql.Replace("$1", $"'{key}'", StringComparison.Ordinal)
                .Replace("$2", now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
        LogGeneratedSql(logger, sql);
        using NpgsqlCommand command = new(sql, connection);
        if (Options.UsePreparedStatements)
        {
            command.Parameters.AddWithValue<string>(NpgsqlDbType.Varchar, key);
            command.Parameters.AddWithValue<long>(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
            command.Prepare();
        }
        using NpgsqlDataReader dataReader = command.ExecuteReader(CommandBehavior.SingleRow | CommandBehavior.SingleResult);

        if (!dataReader.Read())
        {
            LogCacheEntryNotFound(logger, key);
            metrics.Get(key, duration: stopwatch.Elapsed, hit: false, bytesRead: 0);
            return null;
        }

        byte[] value = dataReader.GetFieldValue<byte[]>("Value");

        LogCacheEntryFound(logger, key);
        metrics.Get(key, duration: stopwatch.Elapsed, hit: true, bytesRead: value.Length);
        activity?.Succeed();

        return value;
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string key, CancellationToken token)
    {
        using Activity? activity = PostgresCacheActivitySource.StartGetActivity(key);
        Stopwatch stopwatch = Stopwatch.StartNew();
        DateTimeOffset now = timeProvider.GetUtcNow();

        NpgsqlConnection connection = await connectionFactory.OpenConnectionAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable _connCAD = connection.ConfigureAwait(false);

        string sql = sqlQueries.GetCacheEntry;
        if (!Options.UsePreparedStatements)
        {
            sql = sql.Replace("$1", $"'{key}'", StringComparison.Ordinal)
                .Replace("$2", now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
        LogGeneratedSql(logger, sql);
        NpgsqlCommand command = new(sql, connection);
        await using ConfiguredAsyncDisposable _commCAD = command.ConfigureAwait(false);

        if (Options.UsePreparedStatements)
        {
            command.Parameters.AddWithValue<string>(NpgsqlDbType.Varchar, key);
            command.Parameters.AddWithValue<long>(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
            await command.PrepareAsync(token).ConfigureAwait(false);
        }

        NpgsqlDataReader dataReader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow | CommandBehavior.SingleResult, token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable _drCAD = dataReader.ConfigureAwait(false);

        if (!await dataReader.ReadAsync(token).ConfigureAwait(false))
        {
            LogCacheEntryNotFound(logger, key);
            metrics.Get(key, duration: stopwatch.Elapsed, hit: false, bytesRead: 0);
            return null;
        }

        byte[] value = await dataReader.GetFieldValueAsync<byte[]>(0, token).ConfigureAwait(false);

        LogCacheEntryFound(logger, key);
        metrics.Get(key, duration: stopwatch.Elapsed, hit: true, bytesRead: value.Length);
        activity?.Succeed();

        return value;
    }

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        using Activity? activity = PostgresCacheActivitySource.StartSetActivity(key: key,
            absoluteExpirationRelativeToNow: options.AbsoluteExpirationRelativeToNow,
            slidingExpiration: options.SlidingExpiration);
        Stopwatch stopwatch = Stopwatch.StartNew();
        DateTimeOffset now = timeProvider.GetUtcNow();
        ValidateOptions(options, now);
        TimeSpan? slidingExpiration = GetSlidingExpiration(options);
        DateTimeOffset? absoluteExpiration = GetAbsoluteExpiration(now, options);
        DateTimeOffset expiresAt = ComputeExpiresAt(now, absoluteExpiration, options);
        Debug.Assert(slidingExpiration is not null || absoluteExpiration is not null);

        using NpgsqlConnection connection = connectionFactory.OpenConnection();
        string sql = sqlQueries.SetCacheEntry;
        if (!Options.UsePreparedStatements)
        {
            sql = sql.Replace("$1", $"'{key}'", StringComparison.Ordinal)
                .Replace("$2", $"decode('{Convert.ToBase64String(value)}', 'base64')", StringComparison.Ordinal)
                .Replace("$3", expiresAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("$4", slidingExpiration?.ToMilliseconds().ToString(CultureInfo.InvariantCulture) ?? "NULL", StringComparison.Ordinal)
                .Replace("$5", absoluteExpiration?.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture) ?? "NULL", StringComparison.Ordinal);
        }
        LogGeneratedSql(logger, sql);
        using NpgsqlCommand command = new(sql, connection);
        if (Options.UsePreparedStatements)
        {
            command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
            command.Parameters.AddWithValue(NpgsqlDbType.Bytea, value);
            command.Parameters.AddWithValue(NpgsqlDbType.Bigint, expiresAt.ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue<long?>(NpgsqlDbType.Bigint, slidingExpiration?.ToMilliseconds());
            command.Parameters.AddWithValue<long?>(NpgsqlDbType.Bigint, absoluteExpiration?.ToUnixTimeMilliseconds());
            command.Prepare();
        }
        command.ExecuteNonQuery();

        LogCacheEntryAdded(logger, key);
        metrics.Set(key, duration: stopwatch.Elapsed, bytesWritten: value.Length);
        activity?.Succeed();
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        using Activity? activity = PostgresCacheActivitySource.StartSetActivity(key: key,
            absoluteExpirationRelativeToNow: options.AbsoluteExpirationRelativeToNow,
            slidingExpiration: options.SlidingExpiration);
        Stopwatch stopwatch = Stopwatch.StartNew();
        DateTimeOffset now = timeProvider.GetUtcNow();
        ValidateOptions(options, now);
        TimeSpan? slidingExpiration = GetSlidingExpiration(options);
        DateTimeOffset? absoluteExpiration = GetAbsoluteExpiration(now, options);
        DateTimeOffset expiresAt = ComputeExpiresAt(now, absoluteExpiration, options);
        Debug.Assert(slidingExpiration is not null || absoluteExpiration is not null);

        NpgsqlConnection connection = await connectionFactory.OpenConnectionAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable _connCAD = connection.ConfigureAwait(false);

        string sql = sqlQueries.SetCacheEntry;
        if (!Options.UsePreparedStatements)
        {
            sql = sql.Replace("$1", $"'{key}'", StringComparison.Ordinal)
                .Replace("$2", $"decode('{Convert.ToBase64String(value)}', 'base64')", StringComparison.Ordinal)
                .Replace("$3", expiresAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("$4", slidingExpiration?.ToMilliseconds().ToString(CultureInfo.InvariantCulture) ?? "NULL", StringComparison.Ordinal)
                .Replace("$5", absoluteExpiration?.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture) ?? "NULL", StringComparison.Ordinal);
        }
        LogGeneratedSql(logger, sql);
        NpgsqlCommand command = new(sql, connection);
        await using ConfiguredAsyncDisposable _commCAD = command.ConfigureAwait(false);
        if (Options.UsePreparedStatements)
        {
            command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
            command.Parameters.AddWithValue(NpgsqlDbType.Bytea, value);
            command.Parameters.AddWithValue(NpgsqlDbType.Bigint, expiresAt.ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue<long?>(NpgsqlDbType.Bigint, slidingExpiration?.ToMilliseconds());
            command.Parameters.AddWithValue<long?>(NpgsqlDbType.Bigint, absoluteExpiration?.ToUnixTimeMilliseconds());
            await command.PrepareAsync(token).ConfigureAwait(false);
        }
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);

        LogCacheEntryAdded(logger, key);
        metrics.Set(key, duration: stopwatch.Elapsed, bytesWritten: value.Length);
        activity?.Succeed();
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
        using Activity? activity = PostgresCacheActivitySource.StartRefreshActivity(key);
        Stopwatch stopwatch = Stopwatch.StartNew();
        DateTimeOffset now = timeProvider.GetUtcNow();
        using NpgsqlConnection connection = connectionFactory.OpenConnection();
        string sql = sqlQueries.RefreshCacheEntry;
        if (!Options.UsePreparedStatements)
        {
            sql = sql.Replace("$1", $"'{key}'", StringComparison.Ordinal)
                .Replace("$2", now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
        LogGeneratedSql(logger, sql);
        using NpgsqlCommand command = new(sql, connection);
        if (Options.UsePreparedStatements)
        {
            command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
            command.Parameters.AddWithValue(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
            command.Prepare();
        }
        command.ExecuteNonQuery();

        LogCacheEntryRefreshed(logger, key);
        metrics.Refresh(key, duration: stopwatch.Elapsed);
        activity?.Succeed();
    }

    /// <inheritdoc />
    public async Task RefreshAsync(string key, CancellationToken token)
    {
        using Activity? activity = PostgresCacheActivitySource.StartRefreshActivity(key);
        Stopwatch stopwatch = Stopwatch.StartNew();
        DateTimeOffset now = timeProvider.GetUtcNow();

        NpgsqlConnection connection = await connectionFactory.OpenConnectionAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable _connCAD = connection.ConfigureAwait(false);

        string sql = sqlQueries.RefreshCacheEntry;
        if (!Options.UsePreparedStatements)
        {
            sql = sql.Replace("$1", $"'{key}'", StringComparison.Ordinal)
                .Replace("$2", now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
        LogGeneratedSql(logger, sql);
        NpgsqlCommand command = new(sql, connection);
        await using ConfiguredAsyncDisposable _commCAD = command.ConfigureAwait(false);
        if (Options.UsePreparedStatements)
        {
            command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
            command.Parameters.AddWithValue(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
            await command.PrepareAsync(token).ConfigureAwait(false);
        }
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);

        LogCacheEntryRefreshed(logger, key);
        metrics.Refresh(key, duration: stopwatch.Elapsed);
        activity?.Succeed();
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        using Activity? activity = PostgresCacheActivitySource.StartRemoveActivity(key);
        Stopwatch stopwatch = Stopwatch.StartNew();
        using NpgsqlConnection connection = connectionFactory.OpenConnection();
        string sql = sqlQueries.RemoveCacheEntry;
        if (!Options.UsePreparedStatements)
        {
            sql = sql.Replace("$1", $"'{key}'", StringComparison.Ordinal);
        }
        LogGeneratedSql(logger, sql);
        using NpgsqlCommand command = new(sql, connection);
        if (Options.UsePreparedStatements)
        {
            command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
            command.Prepare();
        }
        command.ExecuteNonQuery();

        LogCacheEntryRemoved(logger, key);
        metrics.Remove(key, duration: stopwatch.Elapsed);
        activity?.Succeed();
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken token)
    {
        using Activity? activity = PostgresCacheActivitySource.StartRemoveActivity(key);
        Stopwatch stopwatch = Stopwatch.StartNew();

        NpgsqlConnection connection = await connectionFactory.OpenConnectionAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable _connCAD = connection.ConfigureAwait(false);

        string sql = sqlQueries.RemoveCacheEntry;
        if (!Options.UsePreparedStatements)
        {
            sql = sql.Replace("$1", $"'{key}'", StringComparison.Ordinal);
        }
        LogGeneratedSql(logger, sql);
        NpgsqlCommand command = new(sql, connection);
        await using ConfiguredAsyncDisposable _commCAD = command.ConfigureAwait(false);
        if (Options.UsePreparedStatements)
        {
            command.Parameters.AddWithValue(NpgsqlDbType.Varchar, key);
            await command.PrepareAsync(token).ConfigureAwait(false);
        }

        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);

        LogCacheEntryRemoved(logger, key);
        metrics.Remove(key, duration: stopwatch.Elapsed);
        activity?.Succeed();
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
        using Activity? activity = PostgresCacheActivitySource.StartGarbageCollectionActivity();
        Stopwatch stopwatch = Stopwatch.StartNew();
        DateTimeOffset now = timeProvider.GetUtcNow();
        LogDeletingExpiredCacheEntries(logger);
        NpgsqlConnection connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable _connCAD = connection.ConfigureAwait(false);

        try
        {
            string sql = sqlQueries.DeleteExpiredCacheEntries;
            if (!Options.UsePreparedStatements)
            {
                sql = sql.Replace("$1", now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            }
            LogGeneratedSql(logger, sql);
            NpgsqlCommand command = new(sql, connection);
            await using ConfiguredAsyncDisposable _commCAD = command.ConfigureAwait(false);
            if (Options.UsePreparedStatements)
            {
                command.Parameters.AddWithValue(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
                await command.PrepareAsync(ct).ConfigureAwait(false);
            }
            int rows = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            LogDeletedExpiredCacheEntries(logger, rows);
            metrics.GarbageCollection(duration: stopwatch.Elapsed, removedEntriesCount: rows);
            activity?.Succeed();
            return;
        }
        catch (NpgsqlException e) when (e.SqlState?.StartsWith(SqlStateTransactionRollbackPrefix, StringComparison.Ordinal) ?? false)
        {
            // can happen
            activity?.AddEvent(new("Transaction Rollback"));
            logger.LogWarning(e, "Transaction rollback detected \"{SqlState}\" during garbage collection", e.SqlState);
        }
        catch (NpgsqlException e)
        {
            logger.LogError(e, "Postgres error \"{SqlState}\" during garbage collection", e.SqlState);
        }

        logger.LogDebug("Retry with lock");

        try
        {
            string sql = sqlQueries.DeleteExpiredCacheEntriesWithLock;
            if (!Options.UsePreparedStatements)
            {
                sql = sql.Replace("$1", now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            }
            LogGeneratedSql(logger, sql);
            NpgsqlCommand command = new(sql, connection);
            await using ConfiguredAsyncDisposable _commCAD = command.ConfigureAwait(false);
            if (Options.UsePreparedStatements)
            {
                command.Parameters.AddWithValue(NpgsqlDbType.Bigint, now.ToUnixTimeMilliseconds());
                await command.PrepareAsync(ct).ConfigureAwait(false);
            }
            int rows = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            LogDeletedExpiredCacheEntries(logger, rows);
            metrics.GarbageCollection(duration: stopwatch.Elapsed, removedEntriesCount: rows);
            activity?.Succeed();
            return;
        }
        catch (NpgsqlException e)
        {
            logger.LogError(e, "Postgres error \"{SqlState}\" during garbage collection", e.SqlState);
        }

        logger.LogWarning("Final retry with truncate");

        try
        {
            NpgsqlCommand command = new(sqlQueries.TruncateCacheEntries, connection);
            await using ConfiguredAsyncDisposable _commCAD = command.ConfigureAwait(false);
            if (Options.UsePreparedStatements)
            {
                await command.PrepareAsync(ct).ConfigureAwait(false);
            }
            int rows = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            LogDeletedExpiredCacheEntries(logger, rows);
            metrics.GarbageCollection(duration: stopwatch.Elapsed, removedEntriesCount: rows);
            activity?.Succeed();
        }
        catch (NpgsqlException e)
        {
            logger.LogError(e, "Postgres error \"{SqlState}\" during garbage collection", e.SqlState);
            throw;
        }
    }

    /// <summary>
    /// Performs the migration of the database.
    /// </summary>
    public async Task MigrateAsync(CancellationToken ct)
    {
        using Activity? activity = PostgresCacheActivitySource.StartMigrationActivity();

        NpgsqlConnection connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable _connCAD = connection.ConfigureAwait(false);
        NpgsqlTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable _txCAD = transaction.ConfigureAwait(false);
        NpgsqlCommand command = new(sqlQueries.Migration, connection, transaction);
        await using ConfiguredAsyncDisposable _commCAD = command.ConfigureAwait(false);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        LogDatabaseMigrated(logger);
        activity?.Succeed();
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

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Deleted {Count} expired cache entries")]
    private static partial void LogDeletedExpiredCacheEntries(ILogger logger, int count);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "Database migrated")]
    private static partial void LogDatabaseMigrated(ILogger logger);

    [LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "Generated SQL: {Sql}")]
    private static partial void LogGeneratedSql(ILogger logger, string sql);
}