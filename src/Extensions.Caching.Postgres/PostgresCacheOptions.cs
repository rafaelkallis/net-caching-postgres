using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;

namespace RafaelKallis.Extensions.Caching.Postgres;

public class PostgresCacheOptions
{
    /// <summary>
    /// The connection string to the Postgres database.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The schema of the table.
    /// </summary>
    public string SchemaName { get; set; } = PostgresCacheConstants.DefaultSchemaName;

    public string TableName { get; set; } = PostgresCacheConstants.DefaultTableName;

    public string Owner { get; set; } = PostgresCacheConstants.DefaultOwner;

    public int KeyMaxLength { get; set; } = PostgresCacheConstants.DefaultKeyMaxLength;

    /// <summary>
    /// The default lifetime of cache entries if neither <see cref="DistributedCacheEntryOptions.AbsoluteExpiration"/> nor <see cref="DistributedCacheEntryOptions.SlidingExpiration"/> is set.
    /// </summary>
    public TimeSpan DefaultSlidingExpiration { get; set; } = TimeSpan.FromSeconds(PostgresCacheConstants.DefaultSlidingExpirationInSeconds);

    public TimeSpan GarbageCollectionInterval { get; set; } = TimeSpan.FromSeconds(PostgresCacheConstants.DefaultGarbageCollectionIntervalInSeconds);

    internal ISystemClock SystemClock { get; set; } = new SystemClock();
}