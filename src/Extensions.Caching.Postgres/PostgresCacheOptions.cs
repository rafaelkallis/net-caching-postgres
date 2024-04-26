using Microsoft.Extensions.Caching.Distributed;

namespace RafaelKallis.Extensions.Caching.Postgres;

public class PostgresCacheOptions
{
    /// <summary>
    /// The connection string to the Postgres database.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The name of the postgres schema.
    /// </summary>
    public string SchemaName { get; set; } = PostgresCacheConstants.DefaultSchemaName;

    /// <summary>
    /// The name of the table.
    /// </summary>
    public string TableName { get; set; } = PostgresCacheConstants.DefaultTableName;

    /// <summary>
    /// Used to set the owner of the schema and table.
    /// Can be a user or a role.
    /// </summary>
    public string Owner { get; set; } = PostgresCacheConstants.DefaultOwner;

    /// <summary>
    /// Maximum length of the key.
    /// </summary>
    public int KeyMaxLength { get; set; } = PostgresCacheConstants.DefaultKeyMaxLength;

    /// <summary>
    /// Whether the database should be migrated on start.
    /// </summary>
    public bool MigrateOnStart { get; set; } = PostgresCacheConstants.DefaultCreateTableOnStart;

    /// <summary>
    /// Whether the table should be unlogged.
    /// Unlogged tables can be up to 10 times faster than logged tables.
    /// Unlogged tables are not crash-safe and are not replicated.
    /// </summary>
    public bool UseUnloggedTable { get; set; } = PostgresCacheConstants.DefaultUseUnloggedTable;

    /// <summary>
    /// The default lifetime of cache entries if neither <see cref="DistributedCacheEntryOptions.AbsoluteExpiration"/> nor <see cref="DistributedCacheEntryOptions.SlidingExpiration"/> is set.
    /// </summary>
    public TimeSpan DefaultSlidingExpiration { get; set; } = TimeSpan.FromSeconds(PostgresCacheConstants.DefaultSlidingExpirationInSeconds);

    /// <summary>
    /// How often the garbage collection should run.
    /// </summary>
    public TimeSpan GarbageCollectionInterval { get; set; } = TimeSpan.FromSeconds(PostgresCacheConstants.DefaultGarbageCollectionIntervalInSeconds);

    internal bool EnableGarbageCollection { get; set; } = true;
    internal bool UncorrelateGarbageCollection { get; set; } = true;
}